import { useState } from 'react';
import { App, Button, Card, DatePicker, Space, Table, Tabs, Typography } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import {
  downloadReportCsv,
  getBrandReport,
  getCategoryReport,
  getSalesReport,
} from '../lib/api.ts';
import { extractProblemMessage } from '../lib/errors.ts';
import { formatRs } from '../lib/orders.ts';
import type {
  BrandReportRow,
  CategoryReportRow,
  ReportParams,
  SalesReportRow,
} from '../lib/types.ts';

const PRESETS: { label: string; value: [Dayjs, Dayjs] }[] = [
  { label: 'Last 7 days', value: [dayjs().subtract(6, 'day').startOf('day'), dayjs().endOf('day')] },
  {
    label: 'Last 30 days',
    value: [dayjs().subtract(29, 'day').startOf('day'), dayjs().endOf('day')],
  },
  { label: 'This month', value: [dayjs().startOf('month'), dayjs().endOf('day')] },
];

/** Simple CSS vertical bar chart (no chart library). */
function VerticalBars({ data }: { data: { label: string; value: number }[] }) {
  const max = Math.max(...data.map((d) => d.value), 1);
  if (data.length === 0) return null;
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'flex-end',
        gap: 4,
        height: 160,
        padding: '8px 0 4px',
        overflowX: 'auto',
      }}
    >
      {data.map((d) => (
        <div
          key={d.label}
          title={`${d.label}: ${formatRs(d.value)}`}
          style={{
            flex: '1 0 14px',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'flex-end',
            height: '100%',
            minWidth: 14,
          }}
        >
          <div
            style={{
              width: '70%',
              height: `${Math.max((d.value / max) * 100, d.value > 0 ? 2 : 0)}%`,
              background: '#1677ff',
              borderRadius: '2px 2px 0 0',
              minHeight: d.value > 0 ? 2 : 0,
            }}
          />
        </div>
      ))}
    </div>
  );
}

/** Simple CSS horizontal bar list (no chart library). */
function HorizontalBars({ data }: { data: { label: string; value: number }[] }) {
  const max = Math.max(...data.map((d) => d.value), 1);
  if (data.length === 0) return null;
  return (
    <div style={{ marginBottom: 16 }}>
      {data.map((d) => (
        <div key={d.label} style={{ display: 'flex', alignItems: 'center', marginBottom: 6 }}>
          <div
            style={{
              width: 180,
              paddingRight: 8,
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              fontSize: 12,
            }}
            title={d.label}
          >
            {d.label}
          </div>
          <div style={{ flex: 1, background: '#f0f0f0', borderRadius: 4, height: 16 }}>
            <div
              style={{
                width: `${(d.value / max) * 100}%`,
                background: '#1677ff',
                height: '100%',
                borderRadius: 4,
                minWidth: d.value > 0 ? 2 : 0,
              }}
            />
          </div>
          <div style={{ width: 120, textAlign: 'right', fontSize: 12 }}>{formatRs(d.value)}</div>
        </div>
      ))}
    </div>
  );
}

export default function ReportsPage() {
  const { message } = App.useApp();
  const [range, setRange] = useState<[Dayjs, Dayjs]>(PRESETS[1].value);
  const [activeTab, setActiveTab] = useState('sales');
  const [downloading, setDownloading] = useState(false);

  const params: ReportParams = {
    from: range[0].format('YYYY-MM-DD'),
    to: range[1].format('YYYY-MM-DD'),
  };

  const salesQuery = useQuery({
    queryKey: ['report-sales', params],
    queryFn: () => getSalesReport(params),
    placeholderData: keepPreviousData,
    enabled: activeTab === 'sales',
  });
  const categoryQuery = useQuery({
    queryKey: ['report-category', params],
    queryFn: () => getCategoryReport(params),
    placeholderData: keepPreviousData,
    enabled: activeTab === 'category',
  });
  const brandQuery = useQuery({
    queryKey: ['report-brand', params],
    queryFn: () => getBrandReport(params),
    placeholderData: keepPreviousData,
    enabled: activeTab === 'brand',
  });

  const download = (report: 'sales' | 'by-category' | 'by-brand') => {
    setDownloading(true);
    downloadReportCsv(report, params)
      .catch((error: unknown) =>
        message.error(extractProblemMessage(error, 'Failed to download CSV')),
      )
      .finally(() => setDownloading(false));
  };

  const salesColumns: ColumnsType<SalesReportRow> = [
    {
      title: 'Period',
      dataIndex: 'period',
      key: 'period',
      render: (p: string) => dayjs(p).format('DD MMM YYYY'),
    },
    { title: 'Orders', dataIndex: 'orders', key: 'orders', width: 120, align: 'right' },
    {
      title: 'Revenue',
      dataIndex: 'revenue',
      key: 'revenue',
      width: 160,
      align: 'right',
      render: (v: number) => formatRs(v),
    },
  ];

  const categoryColumns: ColumnsType<CategoryReportRow> = [
    { title: 'Category', dataIndex: 'categoryName', key: 'categoryName' },
    { title: 'Orders', dataIndex: 'orders', key: 'orders', width: 110, align: 'right' },
    { title: 'Units', dataIndex: 'units', key: 'units', width: 110, align: 'right' },
    {
      title: 'Revenue',
      dataIndex: 'revenue',
      key: 'revenue',
      width: 160,
      align: 'right',
      render: (v: number) => formatRs(v),
    },
  ];

  const brandColumns: ColumnsType<BrandReportRow> = [
    { title: 'Brand', dataIndex: 'brandName', key: 'brandName' },
    { title: 'Orders', dataIndex: 'orders', key: 'orders', width: 110, align: 'right' },
    { title: 'Units', dataIndex: 'units', key: 'units', width: 110, align: 'right' },
    {
      title: 'Revenue',
      dataIndex: 'revenue',
      key: 'revenue',
      width: 160,
      align: 'right',
      render: (v: number) => formatRs(v),
    },
  ];

  const downloadButton = (report: 'sales' | 'by-category' | 'by-brand') => (
    <Button
      icon={<DownloadOutlined />}
      loading={downloading}
      onClick={() => download(report)}
      style={{ marginBottom: 16 }}
    >
      Download CSV
    </Button>
  );

  return (
    <div>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }} wrap>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Reports
        </Typography.Title>
        <DatePicker.RangePicker
          value={range}
          allowClear={false}
          presets={PRESETS}
          onChange={(values) => {
            if (values?.[0] && values[1]) setRange([values[0], values[1]]);
          }}
        />
      </Space>
      <Card>
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={[
            {
              key: 'sales',
              label: 'Sales over time',
              children: (
                <div>
                  {downloadButton('sales')}
                  <VerticalBars
                    data={(salesQuery.data ?? []).map((r) => ({
                      label: dayjs(r.period).format('DD MMM'),
                      value: r.revenue,
                    }))}
                  />
                  <Table<SalesReportRow>
                    rowKey={(r) => r.period}
                    columns={salesColumns}
                    dataSource={salesQuery.data ?? []}
                    loading={salesQuery.isFetching}
                    pagination={false}
                    size="small"
                    locale={{ emptyText: 'No sales in this period' }}
                  />
                </div>
              ),
            },
            {
              key: 'category',
              label: 'By category',
              children: (
                <div>
                  {downloadButton('by-category')}
                  <HorizontalBars
                    data={(categoryQuery.data ?? []).map((r) => ({
                      label: r.categoryName,
                      value: r.revenue,
                    }))}
                  />
                  <Table<CategoryReportRow>
                    rowKey={(r) => r.categoryName}
                    columns={categoryColumns}
                    dataSource={categoryQuery.data ?? []}
                    loading={categoryQuery.isFetching}
                    pagination={false}
                    size="small"
                    locale={{ emptyText: 'No sales in this period' }}
                  />
                </div>
              ),
            },
            {
              key: 'brand',
              label: 'By brand',
              children: (
                <div>
                  {downloadButton('by-brand')}
                  <HorizontalBars
                    data={(brandQuery.data ?? []).map((r) => ({
                      label: r.brandName,
                      value: r.revenue,
                    }))}
                  />
                  <Table<BrandReportRow>
                    rowKey={(r) => r.brandName}
                    columns={brandColumns}
                    dataSource={brandQuery.data ?? []}
                    loading={brandQuery.isFetching}
                    pagination={false}
                    size="small"
                    locale={{ emptyText: 'No sales in this period' }}
                  />
                </div>
              ),
            },
          ]}
        />
      </Card>
    </div>
  );
}
