import { useEffect, useMemo, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  App,
  Button,
  Card,
  Col,
  Empty,
  Input,
  InputNumber,
  Result,
  Row,
  Select,
  Space,
  Spin,
  Switch,
  Typography,
} from 'antd';
import {
  ArrowDownOutlined,
  ArrowLeftOutlined,
  ArrowUpOutlined,
  DeleteOutlined,
  PlusOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Controller,
  useFieldArray,
  useForm,
  type Control,
  type FieldError,
  type Resolver,
} from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  createProduct,
  getProduct,
  listBrands,
  listCategories,
  listCategoryAttributes,
  updateProduct,
} from '../lib/api.ts';
import { slugify } from '../lib/slug.ts';
import type {
  AdminProduct,
  AttributeDefinition,
  Category,
  Id,
  ProductInput,
  ProductStatus,
} from '../lib/types.ts';

// ---------------------------------------------------------------------------
// Form model
// ---------------------------------------------------------------------------

interface ProductFormValues {
  name: string;
  slug: string;
  categoryId: string | undefined;
  brandId: string | null | undefined;
  description: string;
  price: number | null;
  salePrice: number | null;
  stock: number | null;
  status: ProductStatus;
  images: { url: string }[];
  attributes: Record<string, unknown>;
  metaTitle: string;
  metaDescription: string;
}

// ---------------------------------------------------------------------------
// Dynamic zod schema for attribute fields
// ---------------------------------------------------------------------------

function buildAttributeSchema(defs: AttributeDefinition[]) {
  const shape: Record<string, z.ZodType> = {};
  for (const def of defs) {
    const requiredMsg = `${def.name} is required`;
    switch (def.type) {
      case 'Select':
        shape[def.code] = def.isRequired
          ? z
              .union([z.string(), z.null(), z.undefined()])
              .refine((v) => v != null && v !== '', requiredMsg)
          : z.union([z.string(), z.null(), z.undefined()]);
        break;
      case 'MultiSelect':
        shape[def.code] = def.isRequired
          ? z.array(z.string()).min(1, requiredMsg)
          : z.union([z.array(z.string()), z.null(), z.undefined()]);
        break;
      case 'Number':
        shape[def.code] = def.isRequired
          ? z.union([z.number(), z.null(), z.undefined()]).refine((v) => v != null, requiredMsg)
          : z.union([z.number(), z.null(), z.undefined()]);
        break;
      case 'Boolean':
        shape[def.code] = z.union([z.boolean(), z.null(), z.undefined()]);
        break;
      case 'Text':
        shape[def.code] = def.isRequired
          ? z
              .union([z.string(), z.null(), z.undefined()])
              .refine((v) => v != null && v.trim() !== '', requiredMsg)
          : z.union([z.string(), z.null(), z.undefined()]);
        break;
    }
  }
  return z.object(shape);
}

function buildProductSchema(defs: AttributeDefinition[]) {
  return z
    .object({
      name: z.string().trim().min(1, 'Name is required'),
      slug: z
        .string()
        .trim()
        .min(1, 'Slug is required')
        .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Lowercase letters, numbers and hyphens only'),
      categoryId: z
        .union([z.string(), z.null(), z.undefined()])
        .refine((v) => v != null && v !== '', 'Category is required'),
      brandId: z.union([z.string(), z.null(), z.undefined()]),
      description: z.string(),
      price: z
        .union([z.number().min(0, 'Price must be at least 0'), z.null(), z.undefined()])
        .refine((v) => v != null, 'Price is required'),
      salePrice: z.union([
        z.number().min(0, 'Sale price must be at least 0'),
        z.null(),
        z.undefined(),
      ]),
      stock: z
        .union([
          z.number().int('Stock must be a whole number').min(0, 'Stock must be at least 0'),
          z.null(),
          z.undefined(),
        ])
        .refine((v) => v != null, 'Stock is required'),
      status: z.enum(['Draft', 'Active']),
      images: z.array(z.object({ url: z.string().trim().min(1, 'Image URL is required') })),
      attributes: buildAttributeSchema(defs),
      metaTitle: z.string(),
      metaDescription: z.string(),
    })
    .superRefine((values, ctx) => {
      if (values.salePrice != null && values.price != null && values.salePrice >= values.price) {
        ctx.addIssue({
          code: 'custom',
          path: ['salePrice'],
          message: 'Sale price must be lower than the regular price',
        });
      }
    });
}

// ---------------------------------------------------------------------------
// Attribute value conversion (API map <-> form values)
// ---------------------------------------------------------------------------

function toFormAttributes(
  source: Record<string, string>,
  defs: AttributeDefinition[],
): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const def of defs) {
    const raw = source[def.code];
    switch (def.type) {
      case 'Select':
        result[def.code] = raw || undefined;
        break;
      case 'MultiSelect':
        result[def.code] = raw
          ? raw
              .split(',')
              .map((s) => s.trim())
              .filter(Boolean)
          : [];
        break;
      case 'Number': {
        const n = raw != null && raw !== '' ? Number(raw) : null;
        result[def.code] = n != null && !Number.isNaN(n) ? n : null;
        break;
      }
      case 'Boolean':
        result[def.code] = raw === 'true';
        break;
      case 'Text':
        result[def.code] = raw ?? '';
        break;
    }
  }
  return result;
}

function toApiAttributes(
  values: Record<string, unknown>,
  defs: AttributeDefinition[],
): Record<string, string> {
  const result: Record<string, string> = {};
  for (const def of defs) {
    const value = values[def.code];
    switch (def.type) {
      case 'Select':
        if (typeof value === 'string' && value !== '') result[def.code] = value;
        break;
      case 'MultiSelect':
        if (Array.isArray(value) && value.length > 0) result[def.code] = value.join(',');
        break;
      case 'Number':
        if (typeof value === 'number' && !Number.isNaN(value)) result[def.code] = String(value);
        break;
      case 'Boolean':
        if (value != null) result[def.code] = value ? 'true' : 'false';
        break;
      case 'Text':
        if (typeof value === 'string' && value.trim() !== '') result[def.code] = value.trim();
        break;
    }
  }
  return result;
}

// ---------------------------------------------------------------------------
// Small presentational helpers
// ---------------------------------------------------------------------------

function Field({
  label,
  required,
  error,
  children,
}: {
  label: string;
  required?: boolean;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div style={{ marginBottom: 16 }}>
      <div style={{ marginBottom: 4 }}>
        {required && <span style={{ color: '#ff4d4f', marginRight: 4 }}>*</span>}
        <Typography.Text>{label}</Typography.Text>
      </div>
      {children}
      {error && (
        <div style={{ color: '#ff4d4f', fontSize: 12, marginTop: 2 }}>{error}</div>
      )}
    </div>
  );
}

/** Renders a dynamic attribute input based on the attribute definition type. */
function AttributeField({
  def,
  control,
  error,
}: {
  def: AttributeDefinition;
  control: Control<ProductFormValues>;
  error?: string;
}) {
  return (
    <Field label={def.name} required={def.isRequired} error={error}>
      <Controller
        control={control}
        name={`attributes.${def.code}`}
        render={({ field }) => {
          const status = error ? ('error' as const) : undefined;
          switch (def.type) {
            case 'Select':
              return (
                <Select
                  style={{ width: '100%' }}
                  allowClear={!def.isRequired}
                  placeholder={`Select ${def.name.toLowerCase()}`}
                  options={(def.options ?? []).map((o) => ({ value: o, label: o }))}
                  value={(field.value as string | undefined) ?? undefined}
                  onChange={(v) => field.onChange(v ?? undefined)}
                  status={status}
                />
              );
            case 'MultiSelect':
              return (
                <Select
                  mode="multiple"
                  style={{ width: '100%' }}
                  allowClear
                  placeholder={`Select ${def.name.toLowerCase()}`}
                  options={(def.options ?? []).map((o) => ({ value: o, label: o }))}
                  value={(field.value as string[] | undefined) ?? []}
                  onChange={(v) => field.onChange(v)}
                  status={status}
                />
              );
            case 'Number':
              return (
                <InputNumber
                  style={{ width: '100%' }}
                  placeholder={def.name}
                  value={(field.value as number | null) ?? null}
                  onChange={(v) => field.onChange(v)}
                  status={status}
                />
              );
            case 'Boolean':
              return <Switch checked={!!field.value} onChange={(v) => field.onChange(v)} />;
            case 'Text':
              return (
                <Input
                  placeholder={def.name}
                  value={(field.value as string | undefined) ?? ''}
                  onChange={(e) => field.onChange(e.target.value)}
                  status={status}
                />
              );
          }
        }}
      />
    </Field>
  );
}

// ---------------------------------------------------------------------------
// Product form
// ---------------------------------------------------------------------------

interface CategoryOption {
  value: string;
  label: string;
}

function flattenCategoryOptions(
  categories: Category[],
  parentId: Id | null,
  depth: number,
): CategoryOption[] {
  return categories
    .filter((c) => String(c.parentId ?? '') === String(parentId ?? ''))
    .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
    .flatMap((c) => [
      { value: String(c.id), label: `${'— '.repeat(depth)}${c.name}` },
      ...flattenCategoryOptions(categories, c.id, depth + 1),
    ]);
}

function ProductForm({ product }: { product: AdminProduct | null }) {
  const isEdit = product != null;
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const slugEditedRef = useRef(isEdit);

  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: listCategories });
  const brandsQuery = useQuery({ queryKey: ['brands'], queryFn: listBrands });

  const defaultValues = useMemo<ProductFormValues>(
    () => ({
      name: product?.name ?? '',
      slug: product?.slug ?? '',
      categoryId: product?.categoryId != null ? String(product.categoryId) : undefined,
      brandId: product?.brandId != null ? String(product.brandId) : undefined,
      description: product?.description ?? '',
      price: product?.price ?? null,
      salePrice: product?.salePrice ?? null,
      stock: product?.stock ?? 0,
      status: product?.status ?? 'Draft',
      images: (product?.images ?? []).map((url) => ({ url })),
      attributes: {},
      metaTitle: product?.metaTitle ?? '',
      metaDescription: product?.metaDescription ?? '',
    }),
    [product],
  );

  // The zod schema depends on the selected category's attribute definitions,
  // which are only known after the form exists (we watch categoryId). We give
  // react-hook-form a stable resolver that delegates to the latest dynamic one.
  const resolverRef = useRef<Resolver<ProductFormValues> | null>(null);
  const stableResolver = useMemo<Resolver<ProductFormValues>>(
    () => (values, context, options) => resolverRef.current!(values, context, options),
    [],
  );

  const {
    control,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<ProductFormValues>({
    defaultValues,
    resolver: stableResolver,
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  });

  const categoryId = watch('categoryId');

  const attributesQuery = useQuery({
    queryKey: ['attributes', categoryId],
    queryFn: () => listCategoryAttributes(categoryId!),
    enabled: !!categoryId,
  });
  const attributeDefs = useMemo(
    () =>
      [...(attributesQuery.data ?? [])].sort(
        (a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name),
      ),
    [attributesQuery.data],
  );

  const schema = useMemo(() => buildProductSchema(attributeDefs), [attributeDefs]);
  resolverRef.current = useMemo(
    () => zodResolver(schema) as unknown as Resolver<ProductFormValues>,
    [schema],
  );

  // Sync attribute form values whenever the definitions (or category) change.
  useEffect(() => {
    if (!attributesQuery.data) return;
    const source =
      product && String(product.categoryId) === String(categoryId)
        ? (product.attributes ?? {})
        : {};
    setValue('attributes', toFormAttributes(source, attributesQuery.data));
  }, [attributesQuery.data, categoryId, product, setValue]);

  const { fields, append, remove, swap } = useFieldArray({ control, name: 'images' });

  const categoryOptions = useMemo(
    () => flattenCategoryOptions(categoriesQuery.data ?? [], null, 0),
    [categoriesQuery.data],
  );
  const brandOptions = useMemo(
    () => (brandsQuery.data ?? []).map((b) => ({ value: String(b.id), label: b.name })),
    [brandsQuery.data],
  );

  const saveMutation = useMutation({
    mutationFn: (input: ProductInput) =>
      isEdit ? updateProduct(product.id, input) : createProduct(input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      if (isEdit) void queryClient.invalidateQueries({ queryKey: ['product', String(product.id)] });
      message.success(isEdit ? 'Product updated' : 'Product created');
      navigate('/products');
    },
    onError: () => message.error('Failed to save product'),
  });

  const onSubmit = handleSubmit(
    async (values) => {
      const input: ProductInput = {
        name: values.name.trim(),
        slug: values.slug.trim(),
        categoryId: values.categoryId!,
        brandId: values.brandId ?? null,
        description: values.description.trim() || null,
        price: values.price!,
        salePrice: values.salePrice ?? null,
        stock: values.stock!,
        status: values.status,
        images: values.images.map((i) => i.url.trim()).filter(Boolean),
        attributes: toApiAttributes(values.attributes, attributeDefs),
        metaTitle: values.metaTitle.trim() || null,
        metaDescription: values.metaDescription.trim() || null,
      };
      await saveMutation.mutateAsync(input).catch(() => undefined);
    },
    undefined,
  );

  const attrErrors = (errors.attributes ?? {}) as Record<string, FieldError | undefined>;

  return (
    <form
      onSubmit={(e) => {
        void onSubmit(e);
      }}
      noValidate
    >
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }}>
        <Space>
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/products')}>
            Back
          </Button>
          <Typography.Title level={3} style={{ margin: 0 }}>
            {isEdit ? `Edit product: ${product.name}` : 'New product'}
          </Typography.Title>
        </Space>
        <Button
          type="primary"
          htmlType="submit"
          loading={isSubmitting || saveMutation.isPending}
        >
          {isEdit ? 'Save changes' : 'Create product'}
        </Button>
      </Space>

      <Row gutter={16}>
        <Col xs={24} lg={14}>
          <Card title="Basic information" style={{ marginBottom: 16 }}>
            <Field label="Name" required error={errors.name?.message}>
              <Controller
                control={control}
                name="name"
                render={({ field }) => (
                  <Input
                    {...field}
                    placeholder="Product name"
                    status={errors.name ? 'error' : undefined}
                    onChange={(e) => {
                      field.onChange(e);
                      if (!slugEditedRef.current) {
                        setValue('slug', slugify(e.target.value));
                      }
                    }}
                  />
                )}
              />
            </Field>
            <Field label="Slug" required error={errors.slug?.message}>
              <Controller
                control={control}
                name="slug"
                render={({ field }) => (
                  <Input
                    {...field}
                    placeholder="product-slug"
                    status={errors.slug ? 'error' : undefined}
                    onChange={(e) => {
                      slugEditedRef.current = true;
                      field.onChange(e);
                    }}
                  />
                )}
              />
            </Field>
            <Row gutter={16}>
              <Col span={12}>
                <Field label="Category" required error={errors.categoryId?.message}>
                  <Controller
                    control={control}
                    name="categoryId"
                    render={({ field }) => (
                      <Select
                        style={{ width: '100%' }}
                        placeholder="Select category"
                        options={categoryOptions}
                        loading={categoriesQuery.isLoading}
                        showSearch
                        optionFilterProp="label"
                        value={field.value ?? undefined}
                        onChange={(v) => field.onChange(v)}
                        status={errors.categoryId ? 'error' : undefined}
                      />
                    )}
                  />
                </Field>
              </Col>
              <Col span={12}>
                <Field label="Brand" error={errors.brandId?.message}>
                  <Controller
                    control={control}
                    name="brandId"
                    render={({ field }) => (
                      <Select
                        style={{ width: '100%' }}
                        placeholder="Select brand"
                        options={brandOptions}
                        loading={brandsQuery.isLoading}
                        allowClear
                        showSearch
                        optionFilterProp="label"
                        value={field.value ?? undefined}
                        onChange={(v) => field.onChange(v ?? null)}
                      />
                    )}
                  />
                </Field>
              </Col>
            </Row>
            <Field label="Description" error={errors.description?.message}>
              <Controller
                control={control}
                name="description"
                render={({ field }) => (
                  <Input.TextArea {...field} rows={5} placeholder="Product description" />
                )}
              />
            </Field>
          </Card>

          <Card title="Images" style={{ marginBottom: 16 }}>
            {fields.length === 0 && (
              <Typography.Paragraph type="secondary">No images yet.</Typography.Paragraph>
            )}
            {fields.map((imageField, index) => (
              <div key={imageField.id} style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
                <div style={{ flex: 1 }}>
                  <Controller
                    control={control}
                    name={`images.${index}.url`}
                    render={({ field }) => (
                      <Input
                        {...field}
                        placeholder="https://example.com/image.jpg"
                        status={errors.images?.[index]?.url ? 'error' : undefined}
                      />
                    )}
                  />
                  {errors.images?.[index]?.url?.message && (
                    <div style={{ color: '#ff4d4f', fontSize: 12, marginTop: 2 }}>
                      {errors.images[index]?.url?.message}
                    </div>
                  )}
                </div>
                <Button
                  icon={<ArrowUpOutlined />}
                  disabled={index === 0}
                  onClick={() => swap(index, index - 1)}
                  title="Move up"
                />
                <Button
                  icon={<ArrowDownOutlined />}
                  disabled={index === fields.length - 1}
                  onClick={() => swap(index, index + 1)}
                  title="Move down"
                />
                <Button
                  danger
                  icon={<DeleteOutlined />}
                  onClick={() => remove(index)}
                  title="Remove image"
                />
              </div>
            ))}
            <Button icon={<PlusOutlined />} onClick={() => append({ url: '' })}>
              Add image URL
            </Button>
          </Card>

          <Card title="SEO">
            <Field label="Meta title" error={errors.metaTitle?.message}>
              <Controller
                control={control}
                name="metaTitle"
                render={({ field }) => <Input {...field} placeholder="SEO title" />}
              />
            </Field>
            <Field label="Meta description" error={errors.metaDescription?.message}>
              <Controller
                control={control}
                name="metaDescription"
                render={({ field }) => (
                  <Input.TextArea {...field} rows={3} placeholder="SEO description" />
                )}
              />
            </Field>
          </Card>
        </Col>

        <Col xs={24} lg={10}>
          <Card title="Pricing & inventory" style={{ marginBottom: 16 }}>
            <Row gutter={16}>
              <Col span={12}>
                <Field label="Price" required error={errors.price?.message}>
                  <Controller
                    control={control}
                    name="price"
                    render={({ field }) => (
                      <InputNumber
                        style={{ width: '100%' }}
                        min={0}
                        step={0.01}
                        prefix="$"
                        value={field.value}
                        onChange={(v) => field.onChange(v)}
                        status={errors.price ? 'error' : undefined}
                      />
                    )}
                  />
                </Field>
              </Col>
              <Col span={12}>
                <Field label="Sale price" error={errors.salePrice?.message}>
                  <Controller
                    control={control}
                    name="salePrice"
                    render={({ field }) => (
                      <InputNumber
                        style={{ width: '100%' }}
                        min={0}
                        step={0.01}
                        prefix="$"
                        value={field.value}
                        onChange={(v) => field.onChange(v)}
                        status={errors.salePrice ? 'error' : undefined}
                      />
                    )}
                  />
                </Field>
              </Col>
            </Row>
            <Row gutter={16}>
              <Col span={12}>
                <Field label="Stock" required error={errors.stock?.message}>
                  <Controller
                    control={control}
                    name="stock"
                    render={({ field }) => (
                      <InputNumber
                        style={{ width: '100%' }}
                        min={0}
                        step={1}
                        value={field.value}
                        onChange={(v) => field.onChange(v)}
                        status={errors.stock ? 'error' : undefined}
                      />
                    )}
                  />
                </Field>
              </Col>
              <Col span={12}>
                <Field label="Status" required error={errors.status?.message}>
                  <Controller
                    control={control}
                    name="status"
                    render={({ field }) => (
                      <Select
                        style={{ width: '100%' }}
                        options={[
                          { value: 'Draft', label: 'Draft' },
                          { value: 'Active', label: 'Active' },
                        ]}
                        value={field.value}
                        onChange={(v) => field.onChange(v)}
                      />
                    )}
                  />
                </Field>
              </Col>
            </Row>
          </Card>

          <Card title="Attributes">
            {!categoryId ? (
              <Empty description="Select a category to edit its attributes" />
            ) : attributesQuery.isLoading ? (
              <Spin />
            ) : attributeDefs.length === 0 ? (
              <Typography.Paragraph type="secondary">
                This category has no attribute definitions.
              </Typography.Paragraph>
            ) : (
              attributeDefs.map((def) => (
                <AttributeField
                  key={def.code}
                  def={def}
                  control={control}
                  error={attrErrors[def.code]?.message}
                />
              ))
            )}
          </Card>
        </Col>
      </Row>
    </form>
  );
}

// ---------------------------------------------------------------------------
// Page wrapper (create vs edit)
// ---------------------------------------------------------------------------

export default function ProductEditPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = id != null;

  const productQuery = useQuery({
    queryKey: ['product', id],
    queryFn: () => getProduct(id!),
    enabled: isEdit,
  });

  if (isEdit && productQuery.isLoading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 64 }}>
        <Spin size="large" />
      </div>
    );
  }

  if (isEdit && productQuery.isError) {
    return <Result status="404" title="Product not found" />;
  }

  return <ProductForm key={id ?? 'new'} product={productQuery.data ?? null} />;
}
