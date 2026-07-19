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
import { getDashboardSummary } from '../lib/api.ts';
import type { LowStockProduct } from '../lib/types.ts';

export default function DashboardPage() {
  const summaryQuery = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: getDashboardSummary,
  });

  const summary = summaryQuery.data;
  const loading = summaryQuery.isLoading;

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
