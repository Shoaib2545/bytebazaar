import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  App,
  Button,
  Card,
  Col,
  Descriptions,
  Image,
  Input,
  Modal,
  Row,
  Skeleton,
  Space,
  Table,
  Tag,
  Timeline,
  Typography,
} from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { getAdminOrder, updateAdminOrderStatus } from '../lib/api.ts';
import type { OrderItem, OrderStatus } from '../lib/types.ts';
import {
  ORDER_STATUS_ACTIONS,
  ORDER_STATUS_COLORS,
  type OrderStatusAction,
  formatRs,
} from '../lib/orders.ts';

interface ProblemDetails {
  title?: string;
  detail?: string;
}

function extractErrorMessage(error: unknown): string {
  if (error instanceof AxiosError) {
    const data = error.response?.data as ProblemDetails | undefined;
    if (data?.detail) return data.detail;
    if (data?.title) return data.title;
  }
  return 'Failed to update order status';
}

export default function OrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [pendingAction, setPendingAction] = useState<OrderStatusAction | null>(null);
  const [note, setNote] = useState('');

  const orderQuery = useQuery({
    queryKey: ['admin-orders', id],
    queryFn: () => getAdminOrder(id!),
    enabled: !!id,
  });

  const statusMutation = useMutation({
    mutationFn: (input: { status: OrderStatus; note?: string }) =>
      updateAdminOrderStatus(id!, input),
    onSuccess: (detail) => {
      queryClient.setQueryData(['admin-orders', id], detail);
      void queryClient.invalidateQueries({ queryKey: ['admin-orders'] });
      void queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] });
      message.success(`Order marked as ${detail.status}`);
      setPendingAction(null);
      setNote('');
    },
    onError: (error: unknown) => {
      message.error(extractErrorMessage(error));
    },
  });

  const order = orderQuery.data;

  if (orderQuery.isLoading) {
    return <Skeleton active paragraph={{ rows: 8 }} />;
  }

  if (!order) {
    return (
      <div>
        <Typography.Title level={4}>Order not found</Typography.Title>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/orders')}>
          Back to orders
        </Button>
      </div>
    );
  }

  const actions = ORDER_STATUS_ACTIONS[order.status] ?? [];

  const itemColumns: ColumnsType<OrderItem> = [
    {
      title: 'Product',
      key: 'product',
      render: (_, item) => (
        <Space>
          {item.imageUrl ? (
            <Image
              src={item.imageUrl}
              alt={item.name}
              width={48}
              height={48}
              style={{ objectFit: 'contain' }}
              preview={false}
            />
          ) : (
            <div style={{ width: 48, height: 48, background: '#f0f0f0', borderRadius: 4 }} />
          )}
          <Space direction="vertical" size={0}>
            <Typography.Text strong>{item.name}</Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              /{item.slug}
            </Typography.Text>
          </Space>
        </Space>
      ),
    },
    {
      title: 'Unit price',
      dataIndex: 'unitPrice',
      key: 'unitPrice',
      width: 140,
      align: 'right',
      render: (v: number) => formatRs(v),
    },
    {
      title: 'Qty',
      dataIndex: 'quantity',
      key: 'quantity',
      width: 70,
      align: 'right',
    },
    {
      title: 'Line total',
      dataIndex: 'lineTotal',
      key: 'lineTotal',
      width: 140,
      align: 'right',
      render: (v: number) => formatRs(v),
    },
  ];

  return (
    <div>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }} wrap>
        <Space size="middle">
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/orders')} />
          <Typography.Title level={3} style={{ margin: 0 }}>
            {order.orderNumber}
          </Typography.Title>
          <Tag color={ORDER_STATUS_COLORS[order.status]}>{order.status}</Tag>
          <Typography.Text type="secondary">
            {dayjs(order.createdAt).format('DD MMM YYYY, HH:mm')}
          </Typography.Text>
        </Space>
        <Space>
          {actions.map((action) => (
            <Button
              key={action.to}
              type={action.danger ? 'default' : 'primary'}
              danger={action.danger}
              onClick={() => {
                setNote('');
                setPendingAction(action);
              }}
            >
              {action.label}
            </Button>
          ))}
        </Space>
      </Space>

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={16}>
          <Card title="Items" style={{ marginBottom: 16 }}>
            <Table<OrderItem>
              rowKey={(r) => String(r.productId)}
              columns={itemColumns}
              dataSource={order.items}
              pagination={false}
              size="middle"
            />
          </Card>
          <Card title="Status history">
            {order.history.length === 0 ? (
              <Typography.Text type="secondary">No history yet</Typography.Text>
            ) : (
              <Timeline
                items={order.history.map((entry) => ({
                  color: ORDER_STATUS_COLORS[entry.status],
                  children: (
                    <Space direction="vertical" size={0}>
                      <Space>
                        <Typography.Text strong>{entry.status}</Typography.Text>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {dayjs(entry.createdAt).format('DD MMM YYYY, HH:mm')}
                        </Typography.Text>
                      </Space>
                      {entry.note && (
                        <Typography.Text type="secondary">{entry.note}</Typography.Text>
                      )}
                    </Space>
                  ),
                }))}
              />
            )}
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Card title="Totals" style={{ marginBottom: 16 }}>
            <Descriptions column={1} size="small">
              <Descriptions.Item label="Payment">
                <Tag color="purple">{order.paymentMethod}</Tag>
              </Descriptions.Item>
              <Descriptions.Item label="Subtotal">{formatRs(order.subtotal)}</Descriptions.Item>
              <Descriptions.Item label="Shipping">{formatRs(order.shippingFee)}</Descriptions.Item>
              <Descriptions.Item label="Total">
                <Typography.Text strong>{formatRs(order.total)}</Typography.Text>
              </Descriptions.Item>
            </Descriptions>
          </Card>
          <Card title="Shipping address">
            <Descriptions column={1} size="small">
              <Descriptions.Item label="Name">{order.shippingAddress.fullName}</Descriptions.Item>
              <Descriptions.Item label="Phone">{order.shippingAddress.phone}</Descriptions.Item>
              <Descriptions.Item label="Email">{order.shippingAddress.email}</Descriptions.Item>
              <Descriptions.Item label="Address">
                {order.shippingAddress.addressLine}
              </Descriptions.Item>
              <Descriptions.Item label="City">{order.shippingAddress.city}</Descriptions.Item>
              <Descriptions.Item label="Region">{order.shippingAddress.region}</Descriptions.Item>
            </Descriptions>
          </Card>
        </Col>
      </Row>

      <Modal
        title={pendingAction ? `${pendingAction.label} order ${order.orderNumber}?` : ''}
        open={pendingAction !== null}
        okText={pendingAction?.label}
        okButtonProps={{ danger: pendingAction?.danger, loading: statusMutation.isPending }}
        onOk={() => {
          if (pendingAction) {
            statusMutation.mutate({
              status: pendingAction.to,
              note: note.trim() || undefined,
            });
          }
        }}
        onCancel={() => {
          setPendingAction(null);
          setNote('');
        }}
      >
        <Space direction="vertical" style={{ width: '100%' }}>
          <Typography.Text>
            This will change the order status from{' '}
            <Tag color={ORDER_STATUS_COLORS[order.status]}>{order.status}</Tag> to{' '}
            {pendingAction && (
              <Tag color={ORDER_STATUS_COLORS[pendingAction.to]}>{pendingAction.to}</Tag>
            )}
          </Typography.Text>
          <Input.TextArea
            rows={3}
            placeholder="Optional note"
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
        </Space>
      </Modal>
    </div>
  );
}
