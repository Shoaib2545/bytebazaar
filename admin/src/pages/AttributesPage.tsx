import { useMemo, useRef, useState } from 'react';
import {
  App,
  Button,
  Card,
  Empty,
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
  TreeSelect,
  Typography,
} from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import {
  createAttribute,
  deleteAttribute,
  listCategories,
  listCategoryAttributes,
  updateAttribute,
} from '../lib/api.ts';
import { codify } from '../lib/slug.ts';
import type {
  AttributeDefinition,
  AttributeInput,
  AttributeType,
  Category,
  FilterWidget,
  Id,
} from '../lib/types.ts';

const ATTRIBUTE_TYPES: AttributeType[] = ['Select', 'MultiSelect', 'Number', 'Boolean', 'Text'];
const FILTER_WIDGETS: FilterWidget[] = ['Checkbox', 'Radio', 'Range'];

interface AttributeFormValues {
  name: string;
  code: string;
  type: AttributeType;
  options: string[] | undefined;
  isFilterable: boolean;
  isRequired: boolean;
  filterWidget: FilterWidget;
  sortOrder: number;
}

interface CategoryTreeOption {
  value: string;
  title: string;
  children: CategoryTreeOption[];
}

function buildCategoryOptions(categories: Category[], parentId: Id | null): CategoryTreeOption[] {
  return categories
    .filter((c) => String(c.parentId ?? '') === String(parentId ?? ''))
    .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
    .map((c) => ({
      value: String(c.id),
      title: c.name,
      children: buildCategoryOptions(categories, c.id),
    }));
}

export default function AttributesPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [categoryId, setCategoryId] = useState<string | undefined>(undefined);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<AttributeDefinition | null>(null);
  const [form] = Form.useForm<AttributeFormValues>();
  const codeEditedRef = useRef(false);
  const watchedType = Form.useWatch('type', form);

  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: listCategories });
  const attributesQuery = useQuery({
    queryKey: ['attributes', categoryId],
    queryFn: () => listCategoryAttributes(categoryId!),
    enabled: categoryId != null,
  });

  const categoryOptions = useMemo(
    () => buildCategoryOptions(categoriesQuery.data ?? [], null),
    [categoriesQuery.data],
  );

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['attributes', categoryId] });

  const saveMutation = useMutation({
    mutationFn: (payload: { id: Id | null; input: AttributeInput }) =>
      payload.id == null
        ? createAttribute(payload.input)
        : updateAttribute(payload.id, payload.input),
    onSuccess: () => {
      void invalidate();
      setModalOpen(false);
      message.success(editing ? 'Attribute updated' : 'Attribute created');
    },
    onError: () => message.error('Failed to save attribute'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: Id) => deleteAttribute(id),
    onSuccess: () => {
      void invalidate();
      message.success('Attribute deleted');
    },
    onError: () => message.error('Failed to delete attribute'),
  });

  const openCreate = () => {
    setEditing(null);
    codeEditedRef.current = false;
    form.setFieldsValue({
      name: '',
      code: '',
      type: 'Select',
      options: [],
      isFilterable: true,
      isRequired: false,
      filterWidget: 'Checkbox',
      sortOrder: 0,
    });
    setModalOpen(true);
  };

  const openEdit = (attribute: AttributeDefinition) => {
    setEditing(attribute);
    codeEditedRef.current = true;
    form.setFieldsValue({
      name: attribute.name,
      code: attribute.code,
      type: attribute.type,
      options: attribute.options ?? [],
      isFilterable: attribute.isFilterable,
      isRequired: attribute.isRequired,
      filterWidget: attribute.filterWidget,
      sortOrder: attribute.sortOrder,
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    if (!categoryId) return;
    const values = await form.validateFields();
    const hasOptions = values.type === 'Select' || values.type === 'MultiSelect';
    const input: AttributeInput = {
      categoryId,
      name: values.name.trim(),
      code: values.code.trim(),
      type: values.type,
      options: hasOptions ? (values.options ?? []) : [],
      isFilterable: values.isFilterable,
      isRequired: values.isRequired,
      filterWidget: values.filterWidget,
      sortOrder: values.sortOrder ?? 0,
    };
    saveMutation.mutate({ id: editing?.id ?? null, input });
  };

  const columns: ColumnsType<AttributeDefinition> = [
    { title: 'Name', dataIndex: 'name', key: 'name' },
    {
      title: 'Code',
      dataIndex: 'code',
      key: 'code',
      render: (code: string) => <Typography.Text code>{code}</Typography.Text>,
    },
    { title: 'Type', dataIndex: 'type', key: 'type', render: (t: string) => <Tag>{t}</Tag> },
    {
      title: 'Options',
      dataIndex: 'options',
      key: 'options',
      render: (options: string[]) =>
        options?.length ? (
          <Space size={4} wrap>
            {options.map((o) => (
              <Tag key={o}>{o}</Tag>
            ))}
          </Space>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    {
      title: 'Filterable',
      dataIndex: 'isFilterable',
      key: 'isFilterable',
      render: (v: boolean) => (v ? <Tag color="green">Yes</Tag> : <Tag>No</Tag>),
    },
    {
      title: 'Required',
      dataIndex: 'isRequired',
      key: 'isRequired',
      render: (v: boolean) => (v ? <Tag color="red">Yes</Tag> : <Tag>No</Tag>),
    },
    { title: 'Widget', dataIndex: 'filterWidget', key: 'filterWidget' },
    { title: 'Sort', dataIndex: 'sortOrder', key: 'sortOrder', width: 70 },
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
            title="Delete attribute"
            description={`Delete "${record.name}"?`}
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

  const showOptionsField = watchedType === 'Select' || watchedType === 'MultiSelect';

  return (
    <div>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Attributes
        </Typography.Title>
        <Button
          type="primary"
          icon={<PlusOutlined />}
          disabled={!categoryId}
          onClick={openCreate}
        >
          Add attribute
        </Button>
      </Space>
      <Card>
        <Space direction="vertical" style={{ width: '100%' }} size={16}>
          <TreeSelect
            style={{ width: 360 }}
            treeData={categoryOptions}
            value={categoryId}
            onChange={(v) => setCategoryId(v)}
            placeholder="Select a category"
            treeDefaultExpandAll
            showSearch
            treeNodeFilterProp="title"
            loading={categoriesQuery.isLoading}
          />
          {!categoryId ? (
            <Empty description="Select a category to manage its attribute definitions" />
          ) : (
            <Table<AttributeDefinition>
              rowKey={(r) => String(r.id)}
              columns={columns}
              dataSource={attributesQuery.data ?? []}
              loading={attributesQuery.isLoading}
              pagination={false}
              size="middle"
            />
          )}
        </Space>
      </Card>

      <Modal
        title={editing ? 'Edit attribute' : 'New attribute'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        okText="Save"
        confirmLoading={saveMutation.isPending}
        destroyOnHidden
        width={560}
      >
        <Form<AttributeFormValues>
          form={form}
          layout="vertical"
          onValuesChange={(changed) => {
            if (changed.code !== undefined) {
              codeEditedRef.current = true;
            } else if (changed.name !== undefined && !codeEditedRef.current) {
              form.setFieldValue('code', codify(changed.name));
            }
          }}
        >
          <Form.Item
            name="name"
            label="Name"
            rules={[{ required: true, message: 'Name is required' }]}
          >
            <Input placeholder="e.g. Screen Size" />
          </Form.Item>
          <Form.Item
            name="code"
            label="Code"
            rules={[
              { required: true, message: 'Code is required' },
              {
                pattern: /^[a-z0-9]+(_[a-z0-9]+)*$/,
                message: 'Lowercase letters, numbers and underscores only',
              },
            ]}
          >
            <Input placeholder="e.g. screen_size" />
          </Form.Item>
          <Form.Item name="type" label="Type" rules={[{ required: true }]}>
            <Select options={ATTRIBUTE_TYPES.map((t) => ({ value: t, label: t }))} />
          </Form.Item>
          {showOptionsField && (
            <Form.Item
              name="options"
              label="Options"
              rules={[{ required: true, message: 'At least one option is required' }]}
              extra="Type a value and press Enter to add it"
            >
              <Select mode="tags" open={false} suffixIcon={null} placeholder="Add options" />
            </Form.Item>
          )}
          <Space size={24}>
            <Form.Item name="isFilterable" label="Filterable" valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item name="isRequired" label="Required" valuePropName="checked">
              <Switch />
            </Form.Item>
          </Space>
          <Form.Item name="filterWidget" label="Filter widget" rules={[{ required: true }]}>
            <Select options={FILTER_WIDGETS.map((w) => ({ value: w, label: w }))} />
          </Form.Item>
          <Form.Item name="sortOrder" label="Sort order">
            <InputNumber min={0} step={1} style={{ width: 140 }} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
