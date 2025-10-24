-- Daily volume fact table
-- Aggregates transaction volume by tenant, currency, and various dimensions

select
  dt,
  tenant,
  currency,
  src_account_type,
  dst_account_type,
  amount_category,
  hour_of_day,
  day_of_week,
  month_of_year,
  sum(minor) as volume_minor,
  count(*) as tx_count,
  count(distinct src) as unique_senders,
  count(distinct dst) as unique_receivers,
  avg(minor) as avg_amount_minor,
  min(minor) as min_amount_minor,
  max(minor) as max_amount_minor,
  -- Percentiles (ClickHouse specific)
  quantile(0.5)(minor) as median_amount_minor,
  quantile(0.95)(minor) as p95_amount_minor,
  quantile(0.99)(minor) as p99_amount_minor
from {{ ref('stg_tx_events') }}
group by 1, 2, 3, 4, 5, 6, 7, 8, 9
order by dt desc, tenant, currency
