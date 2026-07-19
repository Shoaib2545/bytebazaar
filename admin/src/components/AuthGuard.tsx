import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Button, Result, Spin } from 'antd';
import { hasAdminAccess, useAuth } from '../lib/auth.tsx';

export default function AuthGuard() {
  const { user, loading, signOut } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div
        style={{
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Spin size="large" />
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (!hasAdminAccess(user)) {
    return (
      <Result
        status="403"
        title="403"
        subTitle="Your account does not have access to the admin panel."
        extra={
          <Button type="primary" onClick={() => void signOut()}>
            Sign out
          </Button>
        }
      />
    );
  }

  return <Outlet />;
}
