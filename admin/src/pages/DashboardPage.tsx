import { Link } from 'react-router-dom';
import { Card, Col, Row, Statistic, Table, Typography } from 'antd';
import {
  ClockCircleOutlined,
  DollarOutlined,
  ShopOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { getDashboardSummary } from '../lib/api.ts';
import { formatRs } from '../lib/orders.ts';
import type { DashboardTopProduct, LowStockProduct } from '../lib/types.ts';

/** Simple CSS mini bar chart for the last-7-days revenue. */
function SalesMiniBars({ data }: { data: { date: string; revenue: number }[] }) {
  const max = Math.max(...data.map((d) => d.revenue), 1);
  return (
    <div style={{ display: 'flex', alignItems: 'flex-end', gap: 12, height: 140, padding: 8 }}>
      {data.map((d) => (
        <div
          key={d.date}
          title={`${dayjs(d.date).format('DD MMM')}: ${formatRs(d.revenue)}`}
          style={{
            flex: 1,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'flex-end',
            height: '100%',
          }}
        >
          <Typography.Text type="secondary" style={{ fontSize: 10 }}>
            {formatRs(d.revenue)}
          </Typography.Text>
          <div
            style={{
              width: '60%',
              height: `${Math.max((d.revenue / max) * 100, d.revenue > 0 ? 3 : 0)}%`,
              background: '#1677ff',
              borderRadius: '3px 3px 0 0',
              minHeight: d.revenue > 0 ? 3 : 1,
            }}
          />
          <Typography.Text type="secondary" style={{ fontSize: 11, marginTop: 4 }}>
            {dayjs(d.date).format('DD MMM')}
          </Typography.Text>
        </div>
      ))}
    </div>
  );
}

export default function DashboardPage() {
  const summaryQuery = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: getDashboardSummary,
  });

  const summary = summaryQuery.data;
  const loading = summaryQuery.isLoading;

  const topProductColumns: ColumnsType<DashboardTopProduct> = [
    {
      title: 'Product',
      dataIndex: 'name',
      key: 'name',
      render: (name: string, record) => <Link to={`/products/${record.productId}`}>{name}</Link>,
    },
    {
      title: 'Units',
      dataIndex: 'units',
      key: 'units',
      width: 90,
      align: 'right',
    },
    {
      title: 'Revenue',
      dataIndex: 'revenue',
      key: 'revenue',
      width: 140,
      align: 'right',
      render: (revenue: number) => formatRs(revenue),
    },
  ];

  const lowStockColumns: ColumnsType<LowStockProduct> = [
    {
      title: 'Product',
      dataIndex: 'name',
      key: 'name',
      render: (name: string, record) => <Link to={`/products/${record.id}`}>{name}</Link>,
    },
    {
      title: 'Stock',
      dataIndex: 'stock',
      key: 'stock',
      width: 100,
      align: 'right',
      render: (stock: number) => (
        <Typography.Text type={stock === 0 ? 'danger' : 'warning'} strong>
          {stock}
        </Typography.Text>
      ),
    },
  ];

  return (
    <div>
      <Typography.Title level={3}>Dashboard</Typography.Title>
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Orders today"
              value={summary?.ordersToday ?? 0}
              loading={loading}
              prefix={<ShoppingCartOutlined />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Sales today"
              value={summary?.salesToday ?? 0}
              loading={loading}
              prefix={<DollarOutlined />}
              formatter={(value) =>
                `Rs. ${Number(value).toLocaleString('en-PK', { maximumFractionDigits: 2 })}`
              }
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Pending orders"
              value={summary?.pendingOrders ?? 0}
              loading={loading}
              prefix={<ClockCircleOutlined />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Products"
              value={summary?.totalProducts ?? 0}
              loading={loading}
              prefix={<ShopOutlined />}
            />
          </Card>
        </Col>
      </Row>
      <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
        <Col xs={24} lg={12}>
          <Card title="Sales — last 7 days" loading={loading}>
            <SalesMiniBars data={summary?.salesLast7Days ?? []} />
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card title="Top products (by revenue)">
            <Table<DashboardTopProduct>
              rowKey={(r) => String(r.productId)}
              columns={topProductColumns}
              dataSource={summary?.topProducts ?? []}
              loading={loading}
              pagination={false}
              size="small"
              locale={{ emptyText: 'No sales yet' }}
            />
          </Card>
        </Col>
      </Row>
      <Card title="Low stock (5 or fewer)" style={{ marginTop: 16 }}>
        <Table<LowStockProduct>
          rowKey={(r) => String(r.id)}
          columns={lowStockColumns}
          dataSource={summary?.lowStock ?? []}
          loading={loading}
          pagination={false}
          size="small"
          locale={{ emptyText: 'No low-stock products' }}
        />
      </Card>
    </div>
  );
}
