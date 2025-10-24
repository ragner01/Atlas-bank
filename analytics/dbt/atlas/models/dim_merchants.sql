-- Merchant dimension table
-- Derives merchant attributes and aggregations

with merchant_transactions as (
  select 
    dt,
    tenant,
    src_merchant_id as merchant_id,
    sum(minor) as gross_in_minor,
    count(*) as tx_count,
    count(distinct dst) as unique_customers,
    avg(minor) as avg_tx_amount_minor,
    max(minor) as max_tx_amount_minor,
    min(minor) as min_tx_amount_minor
  from {{ ref('stg_tx_events') }}
  where src_account_type = 'merchant'
  group by 1, 2, 3
  
  union all
  
  select 
    dt,
    tenant,
    dst_merchant_id as merchant_id,
    sum(minor) as gross_in_minor,
    count(*) as tx_count,
    count(distinct src) as unique_customers,
    avg(minor) as avg_tx_amount_minor,
    max(minor) as max_tx_amount_minor,
    min(minor) as min_tx_amount_minor
  from {{ ref('stg_tx_events') }}
  where dst_account_type = 'merchant'
  group by 1, 2, 3
),

merchant_aggregates as (
  select 
    merchant_id,
    tenant,
    sum(gross_in_minor) as total_gross_minor,
    sum(tx_count) as total_tx_count,
    count(distinct dt) as active_days,
    max(dt) as last_activity_date,
    min(dt) as first_activity_date,
    avg(gross_in_minor) as avg_daily_gross_minor,
    avg(tx_count) as avg_daily_tx_count,
    avg(unique_customers) as avg_daily_customers,
    avg(avg_tx_amount_minor) as avg_tx_amount_minor,
    max(max_tx_amount_minor) as max_tx_amount_minor,
    min(min_tx_amount_minor) as min_tx_amount_minor
  from merchant_transactions
  group by 1, 2
)

select 
  merchant_id,
  tenant,
  total_gross_minor,
  total_tx_count,
  active_days,
  last_activity_date,
  first_activity_date,
  avg_daily_gross_minor,
  avg_daily_tx_count,
  avg_daily_customers,
  avg_tx_amount_minor,
  max_tx_amount_minor,
  min_tx_amount_minor,
  -- Merchant tier based on volume
  case 
    when total_gross_minor >= 100000000 then 'enterprise'  -- 1M+ NGN
    when total_gross_minor >= 10000000 then 'premium'      -- 100K+ NGN
    when total_gross_minor >= 1000000 then 'standard'      -- 10K+ NGN
    else 'basic'
  end as merchant_tier,
  -- Trust band based on activity and consistency
  case 
    when active_days >= 30 and avg_daily_tx_count >= 10 then 'high'
    when active_days >= 7 and avg_daily_tx_count >= 5 then 'medium'
    else 'low'
  end as trust_band,
  -- Risk indicators
  case 
    when max_tx_amount_minor > avg_tx_amount_minor * 10 then 'high_variance'
    when avg_daily_customers < 2 then 'low_customer_diversity'
    else 'normal'
  end as risk_indicator
from merchant_aggregates
order by total_gross_minor desc
