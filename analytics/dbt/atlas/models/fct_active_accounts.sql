-- Active accounts fact table
-- Tracks distinct active senders and receivers per day

with base as (
  -- Union all account IDs (both senders and receivers)
  select 
    dt, 
    tenant, 
    src as account_id,
    src_account_type as account_type,
    'sender' as role
  from {{ ref('stg_tx_events') }}
  
  union all
  
  select 
    dt, 
    tenant, 
    dst as account_id,
    dst_account_type as account_type,
    'receiver' as role
  from {{ ref('stg_tx_events') }}
),

account_daily_stats as (
  select 
    dt,
    tenant,
    account_id,
    account_type,
    countIf(role = 'sender') as send_count,
    countIf(role = 'receiver') as receive_count,
    sumIf(role = 'sender', minor) as sent_amount_minor,
    sumIf(role = 'receiver', minor) as received_amount_minor
  from base
  group by 1, 2, 3, 4
)

select 
  dt,
  tenant,
  account_type,
  count(distinct account_id) as active_accounts,
  sum(send_count) as total_sends,
  sum(receive_count) as total_receives,
  sum(sent_amount_minor) as total_sent_minor,
  sum(received_amount_minor) as total_received_minor,
  avg(send_count) as avg_sends_per_account,
  avg(receive_count) as avg_receives_per_account,
  avg(sent_amount_minor) as avg_sent_per_account,
  avg(received_amount_minor) as avg_received_per_account
from account_daily_stats
group by 1, 2, 3
order by dt desc, tenant, account_type
