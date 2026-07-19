import { Navigate, Route, Routes } from 'react-router-dom';
import AuthGuard from './components/AuthGuard.tsx';
import AdminLayout from './components/AdminLayout.tsx';
import LoginPage from './pages/LoginPage.tsx';
import DashboardPage from './pages/DashboardPage.tsx';
import CategoriesPage from './pages/CategoriesPage.tsx';
import AttributesPage from './pages/AttributesPage.tsx';
import BrandsPage from './pages/BrandsPage.tsx';
import ProductsPage from './pages/ProductsPage.tsx';
import ProductEditPage from './pages/ProductEditPage.tsx';
import OrdersPage from './pages/OrdersPage.tsx';
import OrderDetailPage from './pages/OrderDetailPage.tsx';

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<AuthGuard />}>
        <Route element={<AdminLayout />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/orders" element={<OrdersPage />} />
          <Route path="/orders/:id" element={<OrderDetailPage />} />
          <Route path="/categories" element={<CategoriesPage />} />
          <Route path="/attributes" element={<AttributesPage />} />
          <Route path="/brands" element={<BrandsPage />} />
          <Route path="/products" element={<ProductsPage />} />
          <Route path="/products/new" element={<ProductEditPage />} />
          <Route path="/products/:id" element={<ProductEditPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
