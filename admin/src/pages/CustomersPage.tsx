import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Card, Descriptions, Drawer, Input, Skeleton, Table, Tag, Typography } from 'antd';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { getCustomer, listCustomers } from '../lib/api.ts';
import { ORDER_STATUS_COLORS, formatRs } from '../lib/orders.ts';
import type {
  AdminCustomerListItem,
  CustomerRecentOrder,
  Id,
  OrderStatus,
} from '../lib/types.ts';

function CustomerDrawer({ customerId, onClose }: { customerId: Id | null; onClose: () => void }) {
  const detailQuery = useQuery({
    queryKey: ['customer', customerId != null ? String(customerId) : null],
    queryFn: () => getCustomer(customerId!),
    enabled: customerId != null,
  });

  const detail = detailQuery.data;

  const orderColumns: ColumnsType<CustomerRecentOrder> = [
    {
      title: 'Order #',
      dataIndex: 'orderNumber',
      key: 'orderNumber',
      render: (orderNumber: string, record) =>
        record.id != null ? (
          <Link to={`/orders/${record.id}`}>{orderNumber}</Link>
        ) : (
          <Typography.Text strong>{orderNumber}</Typography.Text>
        ),
    },
    {
      title: 'Date',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 150,
      render: (createdAt: string) => dayjs(createdAt).format('DD MMM YYYY, HH:mm'),
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 110,
      render: (s: OrderStatus) => <Tag color={ORDER_STATUS_COLORS[s]}>{s}</Tag>,
    },
    {
      title: 'Total',
      dataIndex: 'total',
      key: 'total',
      width: 120,
      align: 'right',
      render: (total: number) => formatRs(total),
    },
  ];

  return (
    <Drawer
      title={detail?.fullName ?? 'Customer'}
      open={customerId != null}
      onClose={onClose}
      width={640}
    >
      {detailQuery.isLoading ? (
        <Skeleton active paragraph={{ rows: 6 }} />
      ) : detail ? (
        <>
          <Descriptions column={1} size="small" bordered style={{ marginBottom: 24 }}>
            <Descriptions.Item label="Name">{detail.fullName}</Descriptions.Item>
            <Descriptions.Item label="Email">{detail.email}</Descriptions.Item>
            <Descriptions.Item label="Phone">{detail.phone || '—'}</Descriptions.Item>
            <Descriptions.Item label="Orders">{detail.ordersCount}</Descriptions.Item>
            <Descriptions.Item label="Total spent">
              <Typography.Text strong>{formatRs(detail.totalSpent)}</Typography.Text>
            </Descriptions.Item>
          </Descriptions>
          <Typography.Title level={5}>Recent orders</Typography.Title>
          <Table<CustomerRecentOrder>
            rowKey={(r) => r.orderNumber}
            columns={orderColumns}
            dataSource={detail.recentOrders}
            pagination={false}
            size="small"
            locale={{ emptyText: 'No orders yet' }}
          />
        </>
      ) : (
        <Typography.Text type="secondary">Failed to load customer.</Typography.Text>
      )}
    </Drawer>
  );
}

export default function CustomersPage() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState<Id | null>(null);

  const customersQuery = useQuery({
    queryKey: ['customers', { page, pageSize, search }],
    queryFn: () => listCustomers({ page, pageSize, search }),
    placeholderData: keepPreviousData,
  });

  const columns: ColumnsType<AdminCustomerListItem> = [
    {
      title: 'Name',
      dataIndex: 'fullName',
      key: 'fullName',
      render: (name: string) => <Typography.Text strong>{name}</Typography.Text>,
    },
    { title: 'Email', dataIndex: 'email', key: 'email' },
    {
      title: 'Phone',
      dataIndex: 'phone',
      key: 'phone',
      width: 150,
      render: (phone: string | null) => phone || '—',
    },
    {
      title: 'Orders',
      dataIndex: 'ordersCount',
      key: 'ordersCount',
      width: 90,
      align: 'right',
    },
    {
      title: 'Total spent',
      dataIndex: 'totalSpent',
      key: 'totalSpent',
      width: 140,
      align: 'right',
      render: (v: number) => formatRs(v),
    },
  ];

  return (
    <div>
      <Typography.Title level={3} style={{ marginTop: 0 }}>
        Customers
      </Typography.Title>
      <Card>
        <Input.Search
          placeholder="Search by name, email or phone"
          allowClear
          style={{ width: 320, marginBottom: 16 }}
          onSearch={(value) => {
            setSearch(value.trim());
            setPage(1);
          }}
        />
        <Table<AdminCustomerListItem>
          rowKey={(r) => String(r.id)}
          columns={columns}
          dataSource={customersQuery.data?.items ?? []}
          loading={customersQuery.isFetching}
          size="middle"
          onRow={(record) => ({
            onClick: () => setSelectedId(record.id),
            style: { cursor: 'pointer' },
          })}
          pagination={{
            current: page,
            pageSize,
            total: customersQuery.data?.totalCount ?? 0,
            showSizeChanger: true,
            showTotal: (total) => `${total} customers`,
            onChange: (nextPage, nextPageSize) => {
              setPage(nextPageSize !== pageSize ? 1 : nextPage);
              setPageSize(nextPageSize);
            },
          }}
        />
      </Card>
      <CustomerDrawer customerId={selectedId} onClose={() => setSelectedId(null)} />
    </div>
  );
}
