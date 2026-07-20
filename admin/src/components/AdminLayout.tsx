import { useMemo, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Avatar, Dropdown, Layout, Menu, Space, Typography } from 'antd';
import {
  AppstoreOutlined,
  BarChartOutlined,
  DashboardOutlined,
  LogoutOutlined,
  PercentageOutlined,
  PictureOutlined,
  RetweetOutlined,
  SafetyCertificateOutlined,
  ShopOutlined,
  ShoppingCartOutlined,
  TagsOutlined,
  TeamOutlined,
  UnorderedListOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useAuth } from '../lib/auth.tsx';

const { Sider, Header, Content } = Layout;

export default function AdminLayout() {
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, signOut } = useAuth();
  const isAdmin = !!user?.roles.includes('Admin');

  const menuItems = useMemo(
    () => [
      { key: '/', icon: <DashboardOutlined />, label: 'Dashboard' },
      { key: '/orders', icon: <ShoppingCartOutlined />, label: 'Orders' },
      { key: '/categories', icon: <AppstoreOutlined />, label: 'Categories' },
      { key: '/attributes', icon: <UnorderedListOutlined />, label: 'Attributes' },
      { key: '/brands', icon: <TagsOutlined />, label: 'Brands' },
      { key: '/products', icon: <ShopOutlined />, label: 'Products' },
      { key: '/coupons', icon: <PercentageOutlined />, label: 'Coupons' },
      { key: '/banners', icon: <PictureOutlined />, label: 'Banners' },
      { key: '/customers', icon: <TeamOutlined />, label: 'Customers' },
      { key: '/reports', icon: <BarChartOutlined />, label: 'Reports' },
      { key: '/redirects', icon: <RetweetOutlined />, label: 'Redirects' },
      ...(isAdmin
        ? [{ key: '/staff', icon: <SafetyCertificateOutlined />, label: 'Staff' }]
        : []),
    ],
    [isAdmin],
  );

  const selectedKey = useMemo(() => {
    const path = location.pathname;
    if (path === '/') return '/';
    const match = menuItems
      .filter((m) => m.key !== '/' && path.startsWith(m.key))
      .sort((a, b) => b.key.length - a.key.length)[0];
    return match?.key ?? '/';
  }, [location.pathname, menuItems]);

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider collapsible collapsed={collapsed} onCollapse={setCollapsed}>
        <div
          style={{
            height: 48,
            margin: 12,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#fff',
            fontWeight: 700,
            fontSize: collapsed ? 14 : 18,
            letterSpacing: 0.5,
          }}
        >
          {collapsed ? 'BB' : 'ByteBazaar'}
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[selectedKey]}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>
      <Layout>
        <Header
          style={{
            background: '#fff',
            padding: '0 24px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'flex-end',
          }}
        >
          <Dropdown
            menu={{
              items: [
                {
                  key: 'logout',
                  icon: <LogoutOutlined />,
                  label: 'Logout',
                  onClick: () => {
                    void signOut().then(() => navigate('/login'));
                  },
                },
              ],
            }}
          >
            <Space style={{ cursor: 'pointer' }}>
              <Avatar size="small" icon={<UserOutlined />} />
              <Typography.Text strong>{user?.fullName ?? user?.email}</Typography.Text>
            </Space>
          </Dropdown>
        </Header>
        <Content style={{ margin: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
