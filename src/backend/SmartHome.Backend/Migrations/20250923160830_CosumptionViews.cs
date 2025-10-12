using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHome.Backend.Migrations
{
    /// <inheritdoc />
    public partial class CosumptionViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rewritten with CTE to avoid referencing non-grouped raw Timestamp in correlated subquery (PostgreSQL error 42803)
            migrationBuilder.Sql(@"CREATE OR REPLACE VIEW public.daily_powermeter_consumptions AS
WITH base AS (
    SELECT
        t.""PowerMeterId"",
        t.""Timestamp"",
        t.""PowerW"",
        date_trunc('day', t.""Timestamp"") AS day
    FROM public.powermeter_telemetries t
),
integ AS (
    SELECT
        b.""PowerMeterId"",
        b.day,
        COALESCE(b.""PowerW"", 0)::double precision              AS curr_power_w,
        LEAD(COALESCE(b.""PowerW"", 0)) OVER (
            PARTITION BY b.""PowerMeterId"", b.day
            ORDER BY b.""Timestamp""
        )::double precision                                     AS next_power_w,
        EXTRACT(EPOCH FROM LEAD(b.""Timestamp"") OVER (
            PARTITION BY b.""PowerMeterId"", b.day
            ORDER BY b.""Timestamp""
        ) - b.""Timestamp"")::double precision                    AS seconds_delta
    FROM base b
),
clean AS (
    -- scarta intervalli nulli/negativi o troppo lunghi (gap anomali)
    SELECT *
    FROM integ
    WHERE seconds_delta IS NOT NULL
      AND seconds_delta > 0
      AND seconds_delta <= 120  -- se hai campionamento al secondo, 120s è un buon limite anti-outlier
)
SELECT
    ""PowerMeterId"" AS powermeter_id,
    day::date      AS day,
    -- integrazione trapezoidale: ((P_i + P_{i+1})/2) * Δt; W*s / 3600 / 1000 = kWh
    COALESCE(SUM(((curr_power_w + next_power_w) / 2.0) * seconds_delta) / 3600.0 / 1000.0, 0.0)
        AS energy_kwh
FROM clean
GROUP BY ""PowerMeterId"", day;");

            // View for daily appliance consumption integrating power samples (unchanged)
            migrationBuilder.Sql(@"CREATE OR REPLACE VIEW daily_appliance_consumptions AS
SELECT
    a.""ApplianceId"" as appliance_id,
    (date_trunc('day', a.""Timestamp""))::date as day,
    COALESCE(SUM(((lead_power + curr_power)/2.0) * seconds_delta),0)/3600.0/1000.0 as energy_kwh
FROM (
    SELECT
        t.""ApplianceId"",
        t.""Timestamp"",
        COALESCE(t.""PowerWatts"",0) as curr_power,
        LEAD(COALESCE(t.""PowerWatts"",0)) OVER (PARTITION BY date_trunc('day', t.""Timestamp""), t.""ApplianceId"" ORDER BY t.""Timestamp"") as lead_power,
        EXTRACT(EPOCH FROM (LEAD(t.""Timestamp"") OVER (PARTITION BY date_trunc('day', t.""Timestamp""), t.""ApplianceId"" ORDER BY t.""Timestamp"") - t.""Timestamp"")) as seconds_delta
    FROM appliance_telemetries t
) a
WHERE a.seconds_delta IS NOT NULL AND a.seconds_delta > 0 AND a.seconds_delta <= 3600
GROUP BY a.""ApplianceId"", date_trunc('day', a.""Timestamp"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS daily_appliance_consumptions;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS daily_powermeter_consumptions;");
        }
    }
}
