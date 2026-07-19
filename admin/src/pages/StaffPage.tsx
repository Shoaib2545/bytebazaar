import { useState } from 'react';
import {
  App,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import { EditOutlined, KeyOutlined, PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import { createStaff, listStaff, resetStaffPassword, updateStaff } from '../lib/api.ts';
import { extractProblemMessage } from '../lib/errors.ts';
import type { StaffCreateInput, StaffRole, StaffUpdateInput, StaffUser } from '../lib/types.ts';

interface CreateFormValues {
  email: string;
  fullName: string;
  password: string;
  role: StaffRole;
}

interface EditFormValues {
  fullName: string;
  role: StaffRole;
  isActive: boolean;
}

interface ResetFormValues {
  newPassword: string;
}

const ROLE_OPTIONS = [
  { value: 'Admin', label: 'Admin' },
  { value: 'Staff', label: 'Staff' },
];

export default function StaffPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<StaffUser | null>(null);
  const [resetting, setResetting] = useState<StaffUser | null>(null);
  const [createForm] = Form.useForm<CreateFormValues>();
  const [editForm] = Form.useForm<EditFormValues>();
  const [resetForm] = Form.useForm<ResetFormValues>();

  const staffQuery = useQuery({ queryKey: ['staff'], queryFn: listStaff });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['staff'] });

  const createMutation = useMutation({
    mutationFn: (input: StaffCreateInput) => createStaff(input),
    onSuccess: () => {
      void invalidate();
      setCreateOpen(false);
      message.success('Staff member created');
    },
    onError: (error: unknown) =>
      message.error(extractProblemMessage(error, 'Failed to create staff member')),
  });

  const updateMutation = useMutation({
    mutationFn: (payload: { id: StaffUser['id']; input: StaffUpdateInput }) =>
      updateStaff(payload.id, payload.input),
    onSuccess: () => {
      void invalidate();
      setEditing(null);
      message.success('Staff member updated');
    },
    onError: (error: unknown) =>
      message.error(extractProblemMessage(error, 'Failed to update staff member')),
  });

  const resetMutation = useMutation({
    mutationFn: (payload: { id: StaffUser['id']; newPassword: string }) =>
      resetStaffPassword(payload.id, payload.newPassword),
    onSuccess: () => {
      setResetting(null);
      message.success('Password reset');
    },
    onError: (error: unknown) =>
      message.error(extractProblemMessage(error, 'Failed to reset password')),
  });

  const openCreate = () => {
    createForm.setFieldsValue({ email: '', fullName: '', password: '', role: 'Staff' });
    setCreateOpen(true);
  };

  const openEdit = (user: StaffUser) => {
    editForm.setFieldsValue({ fullName: user.fullName, role: user.role, isActive: user.isActive });
    setEditing(user);
  };

  const openReset = (user: StaffUser) => {
    resetForm.setFieldsValue({ newPassword: '' });
    setResetting(user);
  };

  const handleCreate = async () => {
    const values = await createForm.validateFields();
    createMutation.mutate({
      email: values.email.trim(),
      fullName: values.fullName.trim(),
      password: values.password,
      role: values.role,
    });
  };

  const handleEdit = async () => {
    if (!editing) return;
    const values = await editForm.validateFields();
    updateMutation.mutate({
      id: editing.id,
      input: { fullName: values.fullName.trim(), role: values.role, isActive: values.isActive },
    });
  };

  const handleReset = async () => {
    if (!resetting) return;
    const values = await resetForm.validateFields();
    resetMutation.mutate({ id: resetting.id, newPassword: values.newPassword });
  };

  const columns: ColumnsType<StaffUser> = [
    { title: 'Email', dataIndex: 'email', key: 'email' },
    { title: 'Name', dataIndex: 'fullName', key: 'fullName' },
    {
      title: 'Role',
      dataIndex: 'role',
      key: 'role',
      width: 100,
      render: (role: StaffRole) =>
        role === 'Admin' ? <Tag color="gold">Admin</Tag> : <Tag color="blue">Staff</Tag>,
    },
    {
      title: 'Active',
      dataIndex: 'isActive',
      key: 'isActive',
      width: 90,
      render: (isActive: boolean) =>
        isActive ? <Tag color="green">Active</Tag> : <Tag color="default">Disabled</Tag>,
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 120,
      render: (_, record) => (
        <Space>
          <Button
            type="text"
            size="small"
            icon={<EditOutlined />}
            title="Edit"
            onClick={() => openEdit(record)}
          />
          <Button
            type="text"
            size="small"
            icon={<KeyOutlined />}
            title="Reset password"
            onClick={() => openReset(record)}
          />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Staff
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          Add staff member
        </Button>
      </Space>
      <Card>
        <Table<StaffUser>
          rowKey={(r) => String(r.id)}
          columns={columns}
          dataSource={staffQuery.data ?? []}
          loading={staffQuery.isLoading}
          pagination={false}
          size="middle"
        />
      </Card>

      <Modal
        title="New staff member"
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => void handleCreate()}
        okText="Create"
        confirmLoading={createMutation.isPending}
        destroyOnHidden
      >
        <Form<CreateFormValues> form={createForm} layout="vertical">
          <Form.Item
            name="email"
            label="Email"
            rules={[
              { required: true, message: 'Email is required' },
              { type: 'email', message: 'Enter a valid email' },
            ]}
          >
            <Input placeholder="staff@bytebazaar.local" />
          </Form.Item>
          <Form.Item
            name="fullName"
            label="Full name"
            rules={[{ required: true, message: 'Full name is required' }]}
          >
            <Input placeholder="e.g. Jane Doe" />
          </Form.Item>
          <Form.Item
            name="password"
            label="Password"
            rules={[
              { required: true, message: 'Password is required' },
              { min: 8, message: 'At least 8 characters' },
            ]}
          >
            <Input.Password placeholder="Initial password" />
          </Form.Item>
          <Form.Item name="role" label="Role" rules={[{ required: true }]}>
            <Select options={ROLE_OPTIONS} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={editing ? `Edit ${editing.email}` : ''}
        open={editing !== null}
        onCancel={() => setEditing(null)}
        onOk={() => void handleEdit()}
        okText="Save"
        confirmLoading={updateMutation.isPending}
        destroyOnHidden
      >
        <Form<EditFormValues> form={editForm} layout="vertical">
          <Form.Item
            name="fullName"
            label="Full name"
            rules={[{ required: true, message: 'Full name is required' }]}
          >
            <Input />
          </Form.Item>
          <Form.Item name="role" label="Role" rules={[{ required: true }]}>
            <Select options={ROLE_OPTIONS} />
          </Form.Item>
          <Form.Item name="isActive" label="Active" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={resetting ? `Reset password for ${resetting.email}` : ''}
        open={resetting !== null}
        onCancel={() => setResetting(null)}
        onOk={() => void handleReset()}
        okText="Reset password"
        okButtonProps={{ danger: true }}
        confirmLoading={resetMutation.isPending}
        destroyOnHidden
      >
        <Form<ResetFormValues> form={resetForm} layout="vertical">
          <Form.Item
            name="newPassword"
            label="New password"
            rules={[
              { required: true, message: 'New password is required' },
              { min: 8, message: 'At least 8 characters' },
            ]}
          >
            <Input.Password placeholder="New password" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
