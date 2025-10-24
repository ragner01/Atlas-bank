// Add OpenTelemetry tracing to Payments API
builder.Services.AddOpenTelemetry()
  .WithTracing(t => t
    .AddAspNetCoreInstrumentation()
    .AddGrpcClientInstrumentation()
    .AddOtlpExporter());
