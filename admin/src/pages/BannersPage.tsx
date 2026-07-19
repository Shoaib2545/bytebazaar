import { useState } from 'react';
import {
  App,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { createBanner, deleteBanner, listBanners, updateBanner } from '../lib/api.ts';
import { extractProblemMessage } from '../lib/errors.ts';
import type { Banner, BannerInput, BannerPlacement, Id } from '../lib/types.ts';

interface BannerFormValues {
  title: string;
  subtitle: string | undefined;
  imageUrl: string;
  linkUrl: string | undefined;
  placement: BannerPlacement;
  sortOrder: number | null;
  window: [Dayjs | null, Dayjs | null] | null;
  isActive: boolean;
}

function formatWindow(from: string | null, to: string | null): string {
  if (!from && !to) return 'Always';
  const fmt = (v: string | null) => (v ? dayjs(v).format('DD MMM YYYY') : '…');
  return `${fmt(from)} → ${fmt(to)}`;
}

export default function BannersPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Banner | null>(null);
  const [form] = Form.useForm<BannerFormValues>();

  const bannersQuery = useQuery({ queryKey: ['banners'], queryFn: listBanners });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['banners'] });

  const saveMutation = useMutation({
    mutationFn: (payload: { id: Id | null; input: BannerInput }) =>
      payload.id == null ? createBanner(payload.input) : updateBanner(payload.id, payload.input),
    onSuccess: () => {
      void invalidate();
      setModalOpen(false);
      message.success(editing ? 'Banner updated' : 'Banner created');
    },
    onError: (error: unknown) =>
      message.error(extractProblemMessage(error, 'Failed to save banner')),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: Id) => deleteBanner(id),
    onSuccess: () => {
      void invalidate();
      message.success('Banner deleted');
    },
    onError: (error: unknown) =>
      message.error(extractProblemMessage(error, 'Failed to delete banner')),
  });

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      title: '',
      subtitle: undefined,
      imageUrl: '',
      linkUrl: undefined,
      placement: 'Hero',
      sortOrder: 0,
      window: null,
      isActive: true,
    });
    setModalOpen(true);
  };

  const openEdit = (banner: Banner) => {
    setEditing(banner);
    form.setFieldsValue({
      title: banner.title,
      subtitle: banner.subtitle ?? undefined,
      imageUrl: banner.imageUrl,
      linkUrl: banner.linkUrl ?? undefined,
      placement: banner.placement,
      sortOrder: banner.sortOrder,
      window:
        banner.startsAt || banner.endsAt
          ? [
              banner.startsAt ? dayjs(banner.startsAt) : null,
              banner.endsAt ? dayjs(banner.endsAt) : null,
            ]
          : null,
      isActive: banner.isActive,
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    const values = await form.validateFields();
    const [start, end] = values.window ?? [null, null];
    const input: BannerInput = {
      title: values.title.trim(),
      subtitle: values.subtitle?.trim() || null,
      imageUrl: values.imageUrl.trim(),
      linkUrl: values.linkUrl?.trim() || null,
      placement: values.placement,
      sortOrder: values.sortOrder ?? 0,
      isActive: values.isActive,
      startsAt: start ? start.startOf('day').toISOString() : null,
      endsAt: end ? end.endOf('day').toISOString() : null,
    };
    saveMutation.mutate({ id: editing?.id ?? null, input });
  };

  const columns: ColumnsType<Banner> = [
    {
      title: 'Image',
      dataIndex: 'imageUrl',
      key: 'imageUrl',
      width: 110,
      render: (url: string) => (
        <img
          src={url}
          alt=""
          style={{ height: 40, width: 90, objectFit: 'cover', borderRadius: 4 }}
        />
      ),
    },
    {
      title: 'Title',
      dataIndex: 'title',
      key: 'title',
      render: (title: string, record) => (
        <Space direction="vertical" size={0}>
          <Typography.Text strong>{title}</Typography.Text>
          {record.subtitle && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {record.subtitle}
            </Typography.Text>
          )}
        </Space>
      ),
    },
    {
      title: 'Placement',
      dataIndex: 'placement',
      key: 'placement',
      width: 110,
      render: (placement: BannerPlacement) =>
        placement === 'Hero' ? <Tag color="geekblue">Hero</Tag> : <Tag color="cyan">Strip</Tag>,
    },
    {
      title: 'Window',
      key: 'window',
      render: (_, record) => formatWindow(record.startsAt, record.endsAt),
    },
    {
      title: 'Sort',
      dataIndex: 'sortOrder',
      key: 'sortOrder',
      width: 70,
      align: 'right',
    },
    {
      title: 'Active',
      dataIndex: 'isActive',
      key: 'isActive',
      width: 90,
      render: (isActive: boolean) =>
        isActive ? <Tag color="green">Active</Tag> : <Tag color="default">Inactive</Tag>,
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
            title="Delete banner"
            description={`Delete "${record.title}"?`}
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
          Banners
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          Add banner
        </Button>
      </Space>
      <Card>
        <Table<Banner>
          rowKey={(r) => String(r.id)}
          columns={columns}
          dataSource={bannersQuery.data ?? []}
          loading={bannersQuery.isLoading}
          pagination={false}
          size="middle"
        />
      </Card>

      <Modal
        title={editing ? 'Edit banner' : 'New banner'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        okText="Save"
        confirmLoading={saveMutation.isPending}
        destroyOnHidden
      >
        <Form<BannerFormValues> form={form} layout="vertical">
          <Form.Item
            name="title"
            label="Title"
            rules={[{ required: true, message: 'Title is required' }]}
          >
            <Input placeholder="e.g. Summer GPU sale" />
          </Form.Item>
          <Form.Item name="subtitle" label="Subtitle">
            <Input placeholder="Optional subtitle" />
          </Form.Item>
          <Form.Item
            name="imageUrl"
            label="Image URL"
            rules={[{ required: true, message: 'Image URL is required' }]}
          >
            <Input placeholder="https://..." />
          </Form.Item>
          <Form.Item name="linkUrl" label="Link URL">
            <Input placeholder="e.g. /category/graphics-cards" />
          </Form.Item>
          <Space size="middle" style={{ display: 'flex' }} align="start">
            <Form.Item
              name="placement"
              label="Placement"
              rules={[{ required: true }]}
              style={{ minWidth: 140 }}
            >
              <Select
                options={[
                  { value: 'Hero', label: 'Hero' },
                  { value: 'Strip', label: 'Strip' },
                ]}
              />
            </Form.Item>
            <Form.Item name="sortOrder" label="Sort order">
              <InputNumber style={{ width: 120 }} />
            </Form.Item>
            <Form.Item name="isActive" label="Active" valuePropName="checked">
              <Switch />
            </Form.Item>
          </Space>
          <Form.Item name="window" label="Display window">
            <DatePicker.RangePicker
              style={{ width: '100%' }}
              allowEmpty={[true, true]}
              placeholder={['Starts at', 'Ends at']}
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
