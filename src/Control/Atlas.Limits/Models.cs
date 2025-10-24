namespace Atlas.Limits;

/// <summary>
/// Request to check limits for a transaction
/// </summary>
/// <param name="TenantId">Tenant identifier</param>
/// <param name="ActorId">Account/card/customer identifier</param>
/// <param name="DeviceId">Device identifier (optional)</param>
/// <param name="Ip">IP address (optional)</param>
/// <param name="MerchantId">Merchant identifier (optional)</param>
/// <param name="Currency">Currency code</param>
/// <param name="Minor">Amount in minor units</param>
/// <param name="Mcc">Merchant category code</param>
/// <param name="Lat">Latitude for geofencing (optional)</param>
/// <param name="Lng">Longitude for geofencing (optional)</param>
/// <param name="LocalTimeIso">Local time in ISO-8601 format (optional)</param>
public record LimitCheckRequest(
  string TenantId,
  string ActorId,           // account/card/customer
  string? DeviceId,
  string? Ip,
  string? MerchantId,
  string Currency,
  long Minor,
  string Mcc,
  double? Lat = null,         // for geofence
  double? Lng = null,
  string? LocalTimeIso = null // ISO-8601 local time of actor
);

/// <summary>
/// Decision result from limits check
/// </summary>
/// <param name="allowed">Whether the transaction is allowed</param>
/// <param name="action">Action taken (ALLOW, HARD_BLOCK, SOFT_REVIEW)</param>
/// <param name="reason">Reason for the decision</param>
/// <param name="meta">Additional metadata</param>
public record LimitDecision(bool allowed, string action, string reason, IDictionary<string, string>? meta = null);

/// <summary>
/// Policy document containing all limit rules
/// </summary>
/// <param name="Version">Policy version</param>
/// <param name="Velocity">Velocity rules</param>
/// <param name="Mcc">MCC rules</param>
/// <param name="Time">Time window rules</param>
/// <param name="Geo">Geofencing rules</param>
public record PolicyDoc( // policy-as-code JSON (OPA-like, simplified)
  string Version,
  IList<VelocityRule> Velocity,
  IList<MccRule> Mcc,
  IList<TimeRule> Time,
  IList<GeoRule> Geo
);

/// <summary>
/// Velocity limit rule
/// </summary>
/// <param name="id">Rule identifier</param>
/// <param name="scope">Scope: per_actor | per_device | per_merchant</param>
/// <param name="window">Time window (e.g., "1h", "10m", "1d")</param>
/// <param name="currency">Currency code</param>
/// <param name="maxMinor">Maximum amount in minor units</param>
public record VelocityRule(string id, string scope, string window, string currency, long maxMinor);

/// <summary>
/// Merchant category code rule
/// </summary>
/// <param name="id">Rule identifier</param>
/// <param name="allow">Whether to allow or deny</param>
/// <param name="mcc">Array of MCC codes</param>
/// <param name="merchantId">Specific merchant ID (optional)</param>
public record MccRule(string id, bool allow, string[] mcc, string? merchantId = null);

/// <summary>
/// Time window rule
/// </summary>
/// <param name="id">Rule identifier</param>
/// <param name="allow">Whether to allow or deny</param>
/// <param name="cron">Cron expression (simplified: "0-6" for hours)</param>
/// <param name="tz">Time zone</param>
/// <param name="merchantId">Specific merchant ID (optional)</param>
public record TimeRule(string id, bool allow, string cron, string tz, string? merchantId = null);

/// <summary>
/// Geofencing rule
/// </summary>
/// <param name="id">Rule identifier</param>
/// <param name="allow">Whether to allow or deny</param>
/// <param name="polygon">Polygon coordinates as "lat,lng" strings</param>
/// <param name="merchantId">Specific merchant ID (optional)</param>
public record GeoRule(string id, bool allow, string[] polygon, string? merchantId = null);

