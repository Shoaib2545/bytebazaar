import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, Input, Table, Tabs, Tag, Typography } from 'antd';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { listAdminOrders } from '../lib/api.ts';
import type { AdminOrderListItem, OrderStatus } from '../lib/types.ts';
import { ORDER_STATUS_COLORS, ORDER_STATUSES, formatRs } from '../lib/orders.ts';

const statusTabs = [
  { key: 'All', label: 'All' },
  ...ORDER_STATUSES.map((s) => ({ key: s, label: s })),
];

export default function OrdersPage() {
  const navigate = useNavigate();
  const [statusTab, setStatusTab] = useState<string>('All');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const status = statusTab === 'All' ? undefined : (statusTab as OrderStatus);

  const ordersQuery = useQuery({
    queryKey: ['admin-orders', { status, search, page, pageSize }],
    queryFn: () => listAdminOrders({ status, search, page, pageSize }),
    placeholderData: keepPreviousData,
  });

  const columns: ColumnsType<AdminOrderListItem> = [
    {
      title: 'Order #',
      dataIndex: 'orderNumber',
      key: 'orderNumber',
      width: 130,
      render: (orderNumber: string) => <Typography.Text strong>{orderNumber}</Typography.Text>,
    },
    {
      title: 'Date',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 170,
      render: (createdAt: string) => dayjs(createdAt).format('DD MMM YYYY, HH:mm'),
    },
    {
      title: 'Customer',
      dataIndex: 'customerName',
      key: 'customerName',
    },
    {
      title: 'Phone',
      dataIndex: 'phone',
      key: 'phone',
      width: 140,
    },
    {
      title: 'City',
      dataIndex: 'city',
      key: 'city',
      width: 130,
    },
    {
      title: 'Items',
      dataIndex: 'itemCount',
      key: 'itemCount',
      width: 80,
      align: 'right',
    },
    {
      title: 'Total',
      dataIndex: 'total',
      key: 'total',
      width: 130,
      align: 'right',
      render: (total: number) => formatRs(total),
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 110,
      render: (s: OrderStatus) => <Tag color={ORDER_STATUS_COLORS[s]}>{s}</Tag>,
    },
  ];

  return (
    <div>
      <Typography.Title level={3} style={{ marginTop: 0 }}>
        Orders
      </Typography.Title>
      <Card>
        <Tabs
          activeKey={statusTab}
          items={statusTabs}
          onChange={(key) => {
            setStatusTab(key);
            setPage(1);
          }}
        />
        <Input.Search
          placeholder="Search by order #, customer or phone"
          allowClear
          style={{ width: 320, marginBottom: 16 }}
          onSearch={(value) => {
            setSearch(value.trim());
            setPage(1);
          }}
        />
        <Table<AdminOrderListItem>
          rowKey={(r) => String(r.id)}
          columns={columns}
          dataSource={ordersQuery.data?.items ?? []}
          loading={ordersQuery.isFetching}
          size="middle"
          onRow={(record) => ({
            onClick: () => navigate(`/orders/${record.id}`),
            style: { cursor: 'pointer' },
          })}
          pagination={{
            current: page,
            pageSize,
            total: ordersQuery.data?.totalCount ?? 0,
            showSizeChanger: true,
            showTotal: (total) => `${total} orders`,
            onChange: (nextPage, nextPageSize) => {
              setPage(nextPageSize !== pageSize ? 1 : nextPage);
              setPageSize(nextPageSize);
            },
          }}
        />
      </Card>
    </div>
  );
}
