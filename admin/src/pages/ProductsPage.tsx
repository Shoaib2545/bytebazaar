import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  App,
  Button,
  Card,
  Input,
  Popconfirm,
  Space,
  Table,
  Tag,
  TreeSelect,
  Typography,
} from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import { deleteProduct, listBrands, listCategories, listProducts } from '../lib/api.ts';
import type { AdminProductListItem, Category, Id } from '../lib/types.ts';

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

function formatPrice(value: number | null | undefined): string {
  if (value == null) return '—';
  return `$${value.toFixed(2)}`;
}

export default function ProductsPage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [search, setSearch] = useState('');
  const [categoryId, setCategoryId] = useState<string | undefined>(undefined);

  const productsQuery = useQuery({
    queryKey: ['products', { page, pageSize, search, categoryId }],
    queryFn: () => listProducts({ page, pageSize, search, categoryId }),
    placeholderData: keepPreviousData,
  });
  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: listCategories });
  const brandsQuery = useQuery({ queryKey: ['brands'], queryFn: listBrands });

  const categoryNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const c of categoriesQuery.data ?? []) map.set(String(c.id), c.name);
    return map;
  }, [categoriesQuery.data]);

  const brandNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const b of brandsQuery.data ?? []) map.set(String(b.id), b.name);
    return map;
  }, [brandsQuery.data]);

  const categoryOptions = useMemo(
    () => buildCategoryOptions(categoriesQuery.data ?? [], null),
    [categoriesQuery.data],
  );

  const deleteMutation = useMutation({
    mutationFn: (id: Id) => deleteProduct(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      message.success('Product deleted');
    },
    onError: () => message.error('Failed to delete product'),
  });

  const columns: ColumnsType<AdminProductListItem> = [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      render: (name: string, record) => (
        <Space direction="vertical" size={0}>
          <Typography.Text strong>{name}</Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            /{record.slug}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: 'Category',
      key: 'category',
      render: (_, record) =>
        record.categoryName ??
        (record.categoryId != null ? categoryNameById.get(String(record.categoryId)) : undefined) ??
        '—',
    },
    {
      title: 'Brand',
      key: 'brand',
      render: (_, record) =>
        record.brandName ??
        (record.brandId != null ? brandNameById.get(String(record.brandId)) : undefined) ??
        '—',
    },
    {
      title: 'Price',
      key: 'price',
      render: (_, record) => (
        <Space direction="vertical" size={0}>
          <span>{formatPrice(record.price)}</span>
          {record.salePrice != null && (
            <Typography.Text type="success" style={{ fontSize: 12 }}>
              Sale: {formatPrice(record.salePrice)}
            </Typography.Text>
          )}
        </Space>
      ),
    },
    {
      title: 'Stock',
      dataIndex: 'stock',
      key: 'stock',
      width: 90,
      render: (stock: number) =>
        stock > 0 ? stock : <Typography.Text type="danger">0</Typography.Text>,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: string) =>
        status === 'Active' ? <Tag color="green">Active</Tag> : <Tag color="default">Draft</Tag>,
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
            onClick={() => navigate(`/products/${record.id}`)}
          />
          <Popconfirm
            title="Delete product"
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
          Products
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/products/new')}>
          Add product
        </Button>
      </Space>
      <Card>
        <Space style={{ marginBottom: 16 }} wrap>
          <Input.Search
            placeholder="Search products"
            allowClear
            style={{ width: 280 }}
            onSearch={(value) => {
              setSearch(value.trim());
              setPage(1);
            }}
          />
          <TreeSelect
            style={{ width: 260 }}
            treeData={categoryOptions}
            value={categoryId}
            onChange={(v) => {
              setCategoryId(v);
              setPage(1);
            }}
            allowClear
            placeholder="Filter by category"
            treeDefaultExpandAll
            showSearch
            treeNodeFilterProp="title"
          />
        </Space>
        <Table<AdminProductListItem>
          rowKey={(r) => String(r.id)}
          columns={columns}
          dataSource={productsQuery.data?.items ?? []}
          loading={productsQuery.isFetching}
          size="middle"
          pagination={{
            current: page,
            pageSize,
            total: productsQuery.data?.totalCount ?? 0,
            showSizeChanger: true,
            showTotal: (total) => `${total} products`,
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
