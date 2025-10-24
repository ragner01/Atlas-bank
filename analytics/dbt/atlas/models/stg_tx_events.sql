-- Staging model for transaction events
-- Reads from ClickHouse tx_events table or external table over Parquet files

select
  toDate(fromUnixTimestamp64Milli(ts_ms)) as dt,
  ts_ms,
  tenant,
  src,
  dst,
  minor,
  currency,
  entry_id,
  -- Extract account type from account ID
  case 
    when src like 'msisdn::%' then 'mobile'
    when src like 'merchant::%' then 'merchant'
    when src like 'agent::%' then 'agent'
    when src like 'bank::%' then 'bank'
    else 'other'
  end as src_account_type,
  case 
    when dst like 'msisdn::%' then 'mobile'
    when dst like 'merchant::%' then 'merchant'
    when dst like 'agent::%' then 'agent'
    when dst like 'bank::%' then 'bank'
    else 'other'
  end as dst_account_type,
  -- Extract account ID without prefix
  replace(src, 'msisdn::', '') as src_account_id,
  replace(dst, 'msisdn::', '') as dst_account_id,
  replace(src, 'merchant::', '') as src_merchant_id,
  replace(dst, 'merchant::', '') as dst_merchant_id,
  replace(src, 'agent::', '') as src_agent_id,
  replace(dst, 'agent::', '') as dst_agent_id,
  -- Time-based extractions
  toHour(fromUnixTimestamp64Milli(ts_ms)) as hour_of_day,
  toDayOfWeek(fromUnixTimestamp64Milli(ts_ms)) as day_of_week,
  toMonth(fromUnixTimestamp64Milli(ts_ms)) as month_of_year,
  -- Amount categories
  case 
    when minor < 1000 then 'micro'
    when minor < 10000 then 'small'
    when minor < 100000 then 'medium'
    when minor < 1000000 then 'large'
    else 'mega'
  end as amount_category
from tx_events
where ts_ms > 0  -- Filter out invalid timestamps
