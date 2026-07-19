import OrderDetailClient from "./OrderDetailClient";

export const dynamic = "force-dynamic";

interface Props {
  params: Promise<{ orderNumber: string }>;
}

export default async function OrderDetailPage({ params }: Props) {
  const { orderNumber } = await params;
  return <OrderDetailClient orderNumber={decodeURIComponent(orderNumber)} />;
}
