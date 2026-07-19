import ConfirmationClient from "./ConfirmationClient";

export const dynamic = "force-dynamic";

interface Props {
  params: Promise<{ orderNumber: string }>;
}

export default async function OrderConfirmationPage({ params }: Props) {
  const { orderNumber } = await params;
  return <ConfirmationClient orderNumber={decodeURIComponent(orderNumber)} />;
}
