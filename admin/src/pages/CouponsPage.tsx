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
import { createCoupon, deleteCoupon, listCoupons, updateCoupon } from '../lib/api.ts';
import { extractProblemMessage } from '../lib/errors.ts';
import { formatRs } from '../lib/orders.ts';
import type { Coupon, CouponInput, CouponType, Id } from '../lib/types.ts';

interface CouponFormValues {
  code: string;
  type: CouponType;
  value: number | null;
  minOrderAmount: number | null;
  maxUses: number | null;
  window: [Dayjs | null, Dayjs | null] | null;
  isActive: boolean;
}

function formatWindow(from: string | null, to: string | null): string {
  if (!from && !to) return 'Always';
  const fmt = (v: string | null) => (v ? dayjs(v).format('DD MMM YYYY') : '…');
  return `${fmt(from)} → ${fmt(to)}`;
}

function formatCouponValue(coupon: Coupon): string {
  return coupon.type === 'Percent' ? `${coupon.value}%` : formatRs(coupon.value);
}

export default function CouponsPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Coupon | null>(null);
  const [form] = Form.useForm<CouponFormValues>();

  const couponsQuery = useQuery({ queryKey: ['coupons'], queryFn: listCoupons });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['coupons'] });

  const saveMutation = useMutation({
    mutationFn: (payload: { id: Id | null; input: CouponInput }) =>
      payload.id == null ? createCoupon(payload.input) : updateCoupon(payload.id, payload.input),
    onSuccess: () => {
      void invalidate();
      setModalOpen(false);
      message.success(editing ? 'Coupon updated' : 'Coupon created');
    },
    onError: (error: unknown) =>
      message.error(extractProblemMessage(error, 'Failed to save coupon')),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: Id) => deleteCoupon(id),
    onSuccess: () => {
      void invalidate();
      message.success('Coupon deleted');
    },
    onError: (error: unknown) =>
      message.error(extractProblemMessage(error, 'Failed to delete coupon')),
  });

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      code: '',
      type: 'Percent',
      value: null,
      minOrderAmount: null,
      maxUses: null,
      window: null,
      isActive: true,
    });
    setModalOpen(true);
  };

  const openEdit = (coupon: Coupon) => {
    setEditing(coupon);
    form.setFieldsValue({
      code: coupon.code,
      type: coupon.type,
      value: coupon.value,
      minOrderAmount: coupon.minOrderAmount,
      maxUses: coupon.maxUses,
      window:
        coupon.validFrom || coupon.validTo
          ? [
              coupon.validFrom ? dayjs(coupon.validFrom) : null,
              coupon.validTo ? dayjs(coupon.validTo) : null,
            ]
          : null,
      isActive: coupon.isActive,
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    const values = await form.validateFields();
    const [from, to] = values.window ?? [null, null];
    const input: CouponInput = {
      code: values.code.trim().toUpperCase(),
      type: values.type,
      value: values.value!,
      minOrderAmount: values.minOrderAmount ?? null,
      maxUses: values.maxUses ?? null,
      validFrom: from ? from.startOf('day').toISOString() : null,
      validTo: to ? to.endOf('day').toISOString() : null,
      isActive: values.isActive,
    };
    saveMutation.mutate({ id: editing?.id ?? null, input });
  };

  const columns: ColumnsType<Coupon> = [
    {
      title: 'Code',
      dataIndex: 'code',
      key: 'code',
      render: (code: string) => <Typography.Text code strong>{code}</Typography.Text>,
    },
    {
      title: 'Type',
      dataIndex: 'type',
      key: 'type',
      width: 100,
      render: (type: CouponType) =>
        type === 'Percent' ? <Tag color="blue">Percent</Tag> : <Tag color="purple">Fixed</Tag>,
    },
    {
      title: 'Value',
      key: 'value',
      width: 120,
      align: 'right',
      render: (_, record) => formatCouponValue(record),
    },
    {
      title: 'Min order',
      dataIndex: 'minOrderAmount',
      key: 'minOrderAmount',
      width: 130,
      align: 'right',
      render: (v: number | null) => (v != null ? formatRs(v) : '—'),
    },
    {
      title: 'Used / Max',
      key: 'uses',
      width: 110,
      align: 'right',
      render: (_, record) => `${record.usedCount} / ${record.maxUses ?? '∞'}`,
    },
    {
      title: 'Window',
      key: 'window',
      render: (_, record) => formatWindow(record.validFrom, record.validTo),
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
            title="Delete coupon"
            description={`Delete "${record.code}"?`}
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
          Coupons
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          Add coupon
        </Button>
      </Space>
      <Card>
        <Table<Coupon>
          rowKey={(r) => String(r.id)}
          columns={columns}
          dataSource={couponsQuery.data ?? []}
          loading={couponsQuery.isLoading}
          pagination={false}
          size="middle"
        />
      </Card>

      <Modal
        title={editing ? 'Edit coupon' : 'New coupon'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        okText="Save"
        confirmLoading={saveMutation.isPending}
        destroyOnHidden
      >
        <Form<CouponFormValues> form={form} layout="vertical">
          <Form.Item
            name="code"
            label="Code"
            rules={[{ required: true, message: 'Code is required' }]}
            normalize={(value: string) => value?.toUpperCase()}
          >
            <Input placeholder="e.g. SAVE10" style={{ textTransform: 'uppercase' }} />
          </Form.Item>
          <Space size="middle" style={{ display: 'flex' }} align="start">
            <Form.Item
              name="type"
              label="Type"
              rules={[{ required: true }]}
              style={{ minWidth: 140 }}
            >
              <Select
                options={[
                  { value: 'Percent', label: 'Percent (%)' },
                  { value: 'Fixed', label: 'Fixed (Rs.)' },
                ]}
              />
            </Form.Item>
            <Form.Item
              name="value"
              label="Value"
              rules={[{ required: true, message: 'Value is required' }]}
            >
              <InputNumber min={0} style={{ width: 160 }} placeholder="e.g. 10" />
            </Form.Item>
          </Space>
          <Space size="middle" style={{ display: 'flex' }} align="start">
            <Form.Item name="minOrderAmount" label="Min order amount">
              <InputNumber min={0} style={{ width: 180 }} placeholder="No minimum" />
            </Form.Item>
            <Form.Item name="maxUses" label="Max uses">
              <InputNumber min={1} style={{ width: 160 }} placeholder="Unlimited" />
            </Form.Item>
          </Space>
          <Form.Item name="window" label="Valid window">
            <DatePicker.RangePicker
              style={{ width: '100%' }}
              allowEmpty={[true, true]}
              placeholder={['Valid from', 'Valid to']}
            />
          </Form.Item>
          <Form.Item name="isActive" label="Active" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
