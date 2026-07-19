import { useRef, useState } from 'react';
import {
  App,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Popconfirm,
  Space,
  Table,
  Typography,
} from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import { createBrand, deleteBrand, listBrands, updateBrand } from '../lib/api.ts';
import { slugify } from '../lib/slug.ts';
import type { Brand, BrandInput, Id } from '../lib/types.ts';

interface BrandFormValues {
  name: string;
  slug: string;
  logoUrl: string | undefined;
}

export default function BrandsPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Brand | null>(null);
  const [form] = Form.useForm<BrandFormValues>();
  const slugEditedRef = useRef(false);

  const brandsQuery = useQuery({ queryKey: ['brands'], queryFn: listBrands });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['brands'] });

  const saveMutation = useMutation({
    mutationFn: (payload: { id: Id | null; input: BrandInput }) =>
      payload.id == null ? createBrand(payload.input) : updateBrand(payload.id, payload.input),
    onSuccess: () => {
      void invalidate();
      setModalOpen(false);
      message.success(editing ? 'Brand updated' : 'Brand created');
    },
    onError: () => message.error('Failed to save brand'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: Id) => deleteBrand(id),
    onSuccess: () => {
      void invalidate();
      message.success('Brand deleted');
    },
    onError: () => message.error('Failed to delete brand'),
  });

  const openCreate = () => {
    setEditing(null);
    slugEditedRef.current = false;
    form.setFieldsValue({ name: '', slug: '', logoUrl: undefined });
    setModalOpen(true);
  };

  const openEdit = (brand: Brand) => {
    setEditing(brand);
    slugEditedRef.current = true;
    form.setFieldsValue({
      name: brand.name,
      slug: brand.slug,
      logoUrl: brand.logoUrl ?? undefined,
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    const values = await form.validateFields();
    const input: BrandInput = {
      name: values.name.trim(),
      slug: values.slug.trim(),
      logoUrl: values.logoUrl?.trim() || null,
    };
    saveMutation.mutate({ id: editing?.id ?? null, input });
  };

  const columns: ColumnsType<Brand> = [
    {
      title: 'Logo',
      dataIndex: 'logoUrl',
      key: 'logoUrl',
      width: 80,
      render: (url: string | null) =>
        url ? (
          <img src={url} alt="" style={{ height: 28, maxWidth: 64, objectFit: 'contain' }} />
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    { title: 'Name', dataIndex: 'name', key: 'name' },
    {
      title: 'Slug',
      dataIndex: 'slug',
      key: 'slug',
      render: (slug: string) => <Typography.Text code>{slug}</Typography.Text>,
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
            title="Delete brand"
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

  return (
    <div>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Brands
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          Add brand
        </Button>
      </Space>
      <Card>
        <Table<Brand>
          rowKey={(r) => String(r.id)}
          columns={columns}
          dataSource={brandsQuery.data ?? []}
          loading={brandsQuery.isLoading}
          pagination={false}
          size="middle"
        />
      </Card>

      <Modal
        title={editing ? 'Edit brand' : 'New brand'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        okText="Save"
        confirmLoading={saveMutation.isPending}
        destroyOnHidden
      >
        <Form<BrandFormValues>
          form={form}
          layout="vertical"
          onValuesChange={(changed) => {
            if (changed.slug !== undefined) {
              slugEditedRef.current = true;
            } else if (changed.name !== undefined && !slugEditedRef.current) {
              form.setFieldValue('slug', slugify(changed.name));
            }
          }}
        >
          <Form.Item
            name="name"
            label="Name"
            rules={[{ required: true, message: 'Name is required' }]}
          >
            <Input placeholder="e.g. Logitech" />
          </Form.Item>
          <Form.Item
            name="slug"
            label="Slug"
            rules={[
              { required: true, message: 'Slug is required' },
              {
                pattern: /^[a-z0-9]+(-[a-z0-9]+)*$/,
                message: 'Lowercase letters, numbers and hyphens only',
              },
            ]}
          >
            <Input placeholder="e.g. logitech" />
          </Form.Item>
          <Form.Item name="logoUrl" label="Logo URL">
            <Input placeholder="https://..." />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
