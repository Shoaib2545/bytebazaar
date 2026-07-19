import { Card, Col, Row, Statistic, Typography } from 'antd';
import { AppstoreOutlined, ShopOutlined, TagsOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { listBrands, listCategories, listProducts } from '../lib/api.ts';

export default function DashboardPage() {
  const productsQuery = useQuery({
    queryKey: ['products', 'count'],
    queryFn: () => listProducts({ page: 1, pageSize: 1 }),
  });
  const categoriesQuery = useQuery({
    queryKey: ['categories'],
    queryFn: listCategories,
  });
  const brandsQuery = useQuery({
    queryKey: ['brands'],
    queryFn: listBrands,
  });

  return (
    <div>
      <Typography.Title level={3}>Dashboard</Typography.Title>
      <Row gutter={16}>
        <Col xs={24} sm={8}>
          <Card>
            <Statistic
              title="Products"
              value={productsQuery.data?.totalCount ?? 0}
              loading={productsQuery.isLoading}
              prefix={<ShopOutlined />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card>
            <Statistic
              title="Categories"
              value={categoriesQuery.data?.length ?? 0}
              loading={categoriesQuery.isLoading}
              prefix={<AppstoreOutlined />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card>
            <Statistic
              title="Brands"
              value={brandsQuery.data?.length ?? 0}
              loading={brandsQuery.isLoading}
              prefix={<TagsOutlined />}
            />
          </Card>
        </Col>
      </Row>
    </div>
  );
}
