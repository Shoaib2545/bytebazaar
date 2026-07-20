import { useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Popconfirm,
  Space,
  Switch,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import { ArrowRightOutlined, DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import { AxiosError } from 'axios';
import dayjs from 'dayjs';
import { z } from 'zod';
import { createRedirect, deleteRedirect, listRedirects, updateRedirect } from '../lib/api.ts';
import { extractProblemMessage } from '../lib/errors.ts';
import type { Id, Redirect, RedirectInput } from '../lib/types.ts';

interface RedirectFormValues {
  fromPath: string;
  toPath: string;
  isPermanent: boolean;
  isActive: boolean;
}

// ---------------------------------------------------------------------------
// Client-side mirror of the server FluentValidation rules, so the user gets
// inline feedback before submitting. The server remains the source of truth —
// anything it rejects is still surfaced (see handleSubmit's 409 handling).
// ---------------------------------------------------------------------------

/** Server caps both paths at 500 characters (MaximumLength(500)). */
const MAX_PATH_LENGTH = 500;

/** The server accepts any value whose trimmed form starts with http:// or https://. */
const isAbsoluteUrl = (v: string) =>
  v.toLowerCase().startsWith('http://') || v.toLowerCase().startsWith('https://');

/**
 * Mirrors RedirectService.NormalizePath on the backend: trim, drop any query or
 * fragment, strip trailing slashes, force a single leading slash, lowercase.
 * Blank input normalizes to "/". Keep in sync with the C# implementation —
 * stored rules and the server's self-redirect check both use this form.
 */
function normalizePath(path: string | null | undefined): string {
  let value = (path ?? '').trim();
  const cut = value.search(/[?#]/);
  if (cut >= 0) value = value.slice(0, cut);

  value = value.trim().replace(/\/+$/, '');
  if (value.length === 0) return '/';
  if (!value.startsWith('/')) value = `/${value}`;
  return value.toLowerCase();
}

const fromPathSchema = z
  .string()
  .trim()
  .min(1, 'From path is required')
  .max(MAX_PATH_LENGTH, `From path cannot exceed ${MAX_PATH_LENGTH} characters`)
  .refine((v) => v.startsWith('/'), 'From path must be site-relative and start with "/"');

const toPathSchema = z
  .string()
  .trim()
  .min(1, 'To path is required')
  .max(MAX_PATH_LENGTH, `To path cannot exceed ${MAX_PATH_LENGTH} characters`)
  .refine(
    (v) => v.startsWith('/') || isAbsoluteUrl(v),
    'To path must start with "/" or be an absolute http(s) URL',
  );

const redirectSchema = z
  .object({
    fromPath: fromPathSchema,
    toPath: toPathSchema,
    isPermanent: z.boolean(),
    isActive: z.boolean(),
  })
  // Matches the server's `.When(...)`: the self-redirect rule only applies when
  // toPath is site-relative — an absolute http(s) URL can never loop.
  .refine(
    (v) =>
      !v.toPath.startsWith('/') || normalizePath(v.fromPath) !== normalizePath(v.toPath),
    {
      path: ['toPath'],
      message: 'A redirect cannot point to itself',
    },
  );

/** Adapts a zod string schema to an Ant Design Form async validator. */
function zodRule(schema: z.ZodType<string>) {
  return {
    validator: (_rule: unknown, value: string) => {
      const result = schema.safeParse(value ?? '');
      return result.success
        ? Promise.resolve()
        : Promise.reject(new Error(result.error.issues[0]?.message ?? 'Invalid value'));
    },
  };
}

export default function RedirectsPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Redirect | null>(null);
  const [form] = Form.useForm<RedirectFormValues>();

  const redirectsQuery = useQuery({ queryKey: ['redirects'], queryFn: listRedirects });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['redirects'] });

  const saveMutation = useMutation({
    mutationFn: (payload: { id: Id | null; input: RedirectInput }) =>
      payload.id == null ? createRedirect(payload.input) : updateRedirect(payload.id, payload.input),
    onSuccess: () => {
      void invalidate();
      setModalOpen(false);
      message.success(editing ? 'Redirect updated' : 'Redirect created');
    },
    onError: (error: unknown) => {
      // A duplicate fromPath comes back as 409 — render it against the field
      // that caused it rather than as an anonymous toast.
      if (error instanceof AxiosError && error.response?.status === 409) {
        form.setFields([
          {
            name: 'fromPath',
            errors: [
              extractProblemMessage(error, 'A redirect for this "from" path already exists.'),
            ],
          },
        ]);
        return;
      }
      message.error(extractProblemMessage(error, 'Failed to save redirect'));
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: Id) => deleteRedirect(id),
    onSuccess: () => {
      void invalidate();
      message.success('Redirect deleted');
    },
    onError: (error: unknown) =>
      message.error(extractProblemMessage(error, 'Failed to delete redirect')),
  });

  const toggleMutation = useMutation({
    mutationFn: (payload: { redirect: Redirect; isActive: boolean }) =>
      updateRedirect(payload.redirect.id, {
        fromPath: payload.redirect.fromPath,
        toPath: payload.redirect.toPath,
        isPermanent: payload.redirect.isPermanent,
        isActive: payload.isActive,
      }),
    onSuccess: () => void invalidate(),
    onError: (error: unknown) =>
      message.error(extractProblemMessage(error, 'Failed to update redirect')),
  });

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({ fromPath: '', toPath: '', isPermanent: true, isActive: true });
    setModalOpen(true);
  };

  const openEdit = (redirect: Redirect) => {
    setEditing(redirect);
    form.setFieldsValue({
      fromPath: redirect.fromPath,
      toPath: redirect.toPath,
      isPermanent: redirect.isPermanent,
      isActive: redirect.isActive,
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    const values = await form.validateFields();
    const parsed = redirectSchema.safeParse(values);
    if (!parsed.success) {
      // Cross-field rules (self-redirect) land here; attach to their field.
      form.setFields(
        parsed.error.issues.map((issue) => ({
          name: (issue.path[0] as keyof RedirectFormValues) ?? 'fromPath',
          errors: [issue.message],
        })),
      );
      return;
    }
    saveMutation.mutate({ id: editing?.id ?? null, input: parsed.data });
  };

  const columns: ColumnsType<Redirect> = [
    {
      title: 'From → To',
      key: 'paths',
      render: (_, record) => (
        <Space size={8} wrap>
          <Typography.Text code>{record.fromPath}</Typography.Text>
          <ArrowRightOutlined style={{ color: '#8c8c8c' }} />
          <Typography.Text code>{record.toPath}</Typography.Text>
        </Space>
      ),
    },
    {
      title: 'Type',
      dataIndex: 'isPermanent',
      key: 'isPermanent',
      width: 150,
      render: (isPermanent: boolean) =>
        isPermanent ? (
          <Tag color="geekblue">Permanent (301)</Tag>
        ) : (
          <Tag color="cyan">Temporary (302)</Tag>
        ),
    },
    {
      title: 'Active',
      dataIndex: 'isActive',
      key: 'isActive',
      width: 90,
      render: (isActive: boolean, record) => (
        <Switch
          size="small"
          checked={isActive}
          loading={toggleMutation.isPending && toggleMutation.variables?.redirect.id === record.id}
          onChange={(checked) => toggleMutation.mutate({ redirect: record, isActive: checked })}
        />
      ),
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 140,
      render: (createdAt: string) => dayjs(createdAt).format('DD MMM YYYY'),
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 100,
      render: (_, record) => (
        <Space>
          <Button
            type="text"
            size="small"
            icon={<EditOutlined />}
            onClick={() => openEdit(record)}
          />
          <Popconfirm
            title="Delete redirect"
            description={`Delete the redirect from "${record.fromPath}"?`}
            okText="Delete"
            okButtonProps={{ danger: true }}
            onConfirm={() => deleteMutation.mutate(record.id)}
          >
            <Button type="text" size="small" danger icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Redirects
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          Add redirect
        </Button>
      </Space>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Keep old URLs working"
        description='Redirects map retired storefront URLs to their replacements. Use "Permanent (301)" when the old URL is gone for good — search engines transfer ranking to the new URL. Use "Temporary (302)" for short-lived moves.'
      />
      <Card>
        <Table<Redirect>
          rowKey={(r) => String(r.id)}
          columns={columns}
          dataSource={redirectsQuery.data ?? []}
          loading={redirectsQuery.isLoading}
          pagination={false}
          size="middle"
          locale={{ emptyText: 'No redirects configured' }}
        />
      </Card>

      <Modal
        title={editing ? 'Edit redirect' : 'New redirect'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        okText="Save"
        confirmLoading={saveMutation.isPending}
        destroyOnHidden
      >
        <Form<RedirectFormValues> form={form} layout="vertical">
          <Form.Item
            name="fromPath"
            label="From path"
            tooltip="The old, site-relative path visitors still request."
            rules={[zodRule(fromPathSchema)]}
            validateTrigger={['onBlur', 'onSubmit']}
          >
            <Input placeholder="/old-category/gpus" />
          </Form.Item>
          <Form.Item
            name="toPath"
            label="To path or URL"
            tooltip="Where to send them — a site-relative path or an absolute http(s) URL."
            rules={[zodRule(toPathSchema)]}
            validateTrigger={['onBlur', 'onSubmit']}
          >
            <Input placeholder="/category/graphics-cards" />
          </Form.Item>
          <Space size="large">
            <Form.Item name="isPermanent" label="Permanent (301)" valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item name="isActive" label="Active" valuePropName="checked">
              <Tooltip title="Inactive redirects are kept but not served.">
                <Switch />
              </Tooltip>
            </Form.Item>
          </Space>
        </Form>
      </Modal>
    </div>
  );
}
