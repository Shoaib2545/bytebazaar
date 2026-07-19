import { Outlet, useNavigate } from 'react-router-dom';
import { Button, Result } from 'antd';
import { useAuth } from '../lib/auth.tsx';

/** Route guard: only users with the Admin role may pass (Staff get a 403 result). */
export default function RequireAdmin() {
  const { user } = useAuth();
  const navigate = useNavigate();

  if (!user?.roles.includes('Admin')) {
    return (
      <Result
        status="403"
        title="403"
        subTitle="Only administrators can access this page."
        extra={
          <Button type="primary" onClick={() => navigate('/')}>
            Back to dashboard
          </Button>
        }
      />
    );
  }

  return <Outlet />;
}
