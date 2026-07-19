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
  Space,
  Spin,
  Switch,
  Tag,
  Tree,
  TreeSelect,
  Typography,
} from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { TreeDataNode } from 'antd';
import { createCategory, deleteCategory, listCategories, updateCategory } from '../lib/api.ts';
import { slugify } from '../lib/slug.ts';
import type { Category, CategoryInput, Id } from '../lib/types.ts';

interface CategoryFormValues {
  name: string;
  slug: string;
  parentId: Id | null | undefined;
  sortOrder: number;
  isActive: boolean;
  imageUrl: string | undefined;
  metaTitle: string | undefined;
  metaDescription: string | undefined;
}

interface TreeSelectNode {
  value: string;
  title: string;
  children: TreeSelectNode[];
}

function childrenOf(categories: Category[], parentId: Id | null): Category[] {
  return categories
    .filter((c) => String(c.parentId ?? '') === String(parentId ?? ''))
    .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
}

/** ids of a category and all of its descendants (to exclude from parent options). */
function subtreeIds(categories: Category[], rootId: Id): Set<string> {
  const ids = new Set<string>([String(rootId)]);
  let grew = true;
  while (grew) {
    grew = false;
    for (const c of categories) {
      if (c.parentId != null && ids.has(String(c.parentId)) && !ids.has(String(c.id))) {
        ids.add(String(c.id));
        grew = true;
      }
    }
  }
  return ids;
}

export default function CategoriesPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Category | null>(null);
  const [form] = Form.useForm<CategoryFormValues>();
  const slugEditedRef = useRef(false);

  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: listCategories });
  const categories = useMemo(() => categoriesQuery.data ?? [], [categoriesQuery.data]);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['categories'] });

  const saveMutation = useMutation({
    mutationFn: (payload: { id: Id | null; input: CategoryInput }) =>
      payload.id == null
        ? createCategory(payload.input)
        : updateCategory(payload.id, payload.input),
    onSuccess: () => {
      void invalidate();
      setModalOpen(false);
      message.success(editing ? 'Category updated' : 'Category created');
    },
    onError: () => message.error('Failed to save category'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: Id) => deleteCategory(id),
    onSuccess: () => {
      void invalidate();
      message.success('Category deleted');
    },
    onError: () => message.error('Failed to delete category'),
  });

  const openCreate = (parentId: Id | null) => {
    setEditing(null);
    slugEditedRef.current = false;
    form.setFieldsValue({
      name: '',
      slug: '',
      parentId: parentId != null ? String(parentId) : undefined,
      sortOrder: 0,
      isActive: true,
      imageUrl: undefined,
      metaTitle: undefined,
      metaDescription: undefined,
    });
    setModalOpen(true);
  };

  const openEdit = (category: Category) => {
    setEditing(category);
    slugEditedRef.current = true;
    form.setFieldsValue({
      name: category.name,
      slug: category.slug,
      parentId: category.parentId != null ? String(category.parentId) : undefined,
      sortOrder: category.sortOrder,
      isActive: category.isActive,
      imageUrl: category.imageUrl ?? undefined,
      metaTitle: category.metaTitle ?? undefined,
      metaDescription: category.metaDescription ?? undefined,
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    const values = await form.validateFields();
    const input: CategoryInput = {
      name: values.name.trim(),
      slug: values.slug.trim(),
      parentId: values.parentId ?? null,
      imageUrl: values.imageUrl?.trim() || null,
      sortOrder: values.sortOrder ?? 0,
      isActive: values.isActive,
      metaTitle: values.metaTitle?.trim() || null,
      metaDescription: values.metaDescription?.trim() || null,
    };
    saveMutation.mutate({ id: editing?.id ?? null, input });
  };

  const buildTreeNodes = (parentId: Id | null): TreeDataNode[] =>
    childrenOf(categories, parentId).map((category) => ({
      key: String(category.id),
      title: (
        <Space size={8}>
          <span>{category.name}</span>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            /{category.slug}
          </Typography.Text>
          {!category.isActive && <Tag color="orange">inactive</Tag>}
          <Button
            type="text"
            size="small"
            icon={<PlusOutlined />}
            title="Add child category"
            onClick={(e) => {
              e.stopPropagation();
              openCreate(category.id);
            }}
          />
          <Button
            type="text"
            size="small"
            icon={<EditOutlined />}
            title="Edit category"
            onClick={(e) => {
              e.stopPropagation();
              openEdit(category);
            }}
          />
          <Popconfirm
            title="Delete category"
            description={`Delete "${category.name}"?`}
            okText="Delete"
            okButtonProps={{ danger: true }}
            onConfirm={() => deleteMutation.mutate(category.id)}
          >
            <Button
              type="text"
              size="small"
              danger
              icon={<DeleteOutlined />}
              title="Delete category"
              onClick={(e) => e.stopPropagation()}
            />
          </Popconfirm>
        </Space>
      ),
      children: buildTreeNodes(category.id),
    }));

  const treeData = useMemo(
    () => buildTreeNodes(null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [categories],
  );

  const parentOptions = useMemo(() => {
    const excluded = editing ? subtreeIds(categories, editing.id) : new Set<string>();
    const build = (parentId: Id | null): TreeSelectNode[] =>
      childrenOf(categories, parentId)
        .filter((c) => !excluded.has(String(c.id)))
        .map((c) => ({
          value: String(c.id),
          title: c.name,
          children: build(c.id),
        }));
    return build(null);
  }, [categories, editing]);

  return (
    <div>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Categories
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openCreate(null)}>
          Add category
        </Button>
      </Space>
      <Card>
        {categoriesQuery.isLoading ? (
          <Spin />
        ) : treeData.length === 0 ? (
          <Empty description="No categories yet" />
        ) : (
          <Tree treeData={treeData} defaultExpandAll selectable={false} showLine />
        )}
      </Card>

      <Modal
        title={editing ? 'Edit category' : 'New category'}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        okText="Save"
        confirmLoading={saveMutation.isPending}
        destroyOnHidden
        width={560}
      >
        <Form<CategoryFormValues>
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
            <Input placeholder="e.g. Laptops" />
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
            <Input placeholder="e.g. laptops" />
          </Form.Item>
          <Form.Item name="parentId" label="Parent category">
            <TreeSelect
              treeData={parentOptions}
              allowClear
              placeholder="None (top level)"
              treeDefaultExpandAll
              showSearch
              treeNodeFilterProp="title"
            />
          </Form.Item>
          <Space size={24}>
            <Form.Item name="sortOrder" label="Sort order">
              <InputNumber min={0} step={1} style={{ width: 140 }} />
            </Form.Item>
            <Form.Item name="isActive" label="Active" valuePropName="checked">
              <Switch />
            </Form.Item>
          </Space>
          <Form.Item name="imageUrl" label="Image URL">
            <Input placeholder="https://..." />
          </Form.Item>
          <Form.Item name="metaTitle" label="Meta title">
            <Input placeholder="SEO title" />
          </Form.Item>
          <Form.Item name="metaDescription" label="Meta description">
            <Input.TextArea rows={2} placeholder="SEO description" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
