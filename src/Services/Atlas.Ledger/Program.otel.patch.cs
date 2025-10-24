// Add OpenTelemetry tracing to Ledger API
builder.Services.AddOpenTelemetry()
  .WithTracing(t => t
    .AddAspNetCoreInstrumentation()
    .AddGrpcClientInstrumentation()
    .AddOtlpExporter());
