=== FILE: apps/portal-next/package.json ===
{
  "name": "atlas-portal",
  "private": true,
  "version": "0.1.0",
  "scripts": {
    "dev": "next dev",
    "build": "next build",
    "start": "next start",
    "lint": "next lint"
  },
  "dependencies": {
    "@atlasbank/sdk": "file:../../sdks/typescript",
    "next": "14.2.14",
    "react": "18.2.0",
    "react-dom": "18.2.0"
  },
  "devDependencies": {
    "@types/node": "20.14.10",
    "@types/react": "18.2.45",
    "@types/react-dom": "18.2.18",
    "typescript": "5.6.3",
    "eslint": "8.57.0",
    "eslint-config-next": "14.2.14"
  }
}

=== FILE: apps/portal-next/next.config.mjs ===
/** @type {import('next').NextConfig} */
const nextConfig = { reactStrictMode: true };
export default nextConfig;

=== FILE: apps/portal-next/tsconfig.json ===
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022", "DOM"],
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "jsx": "preserve",
    "allowJs": false,
    "strict": true,
    "noEmit": true,
    "paths": { "@/*": ["./*"] },
    "types": ["node"]
  },
  "include": ["**/*.ts", "**/*.tsx"]
}

=== FILE: apps/portal-next/.env.example ===
NEXT_PUBLIC_PAYMENTS_BASE=http://localhost:5191
NEXT_PUBLIC_LIMITS_BASE=http://localhost:5901
NEXT_PUBLIC_TRUST_PORTAL=http://localhost:5802
NEXT_PUBLIC_TENANT=tnt_demo

=== FILE: apps/portal-next/app/layout.tsx ===
import "./globals.css";
export const metadata = { title: "Atlas Portal", description: "Ops & Merchant portal" };
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}

=== FILE: apps/portal-next/app/globals.css ===
:root { --bg:#0b1220; --card:#141f36; --muted:#9fb3ff; --text:#ecf2ff; --accent:#22c55e; }
*{box-sizing:border-box} body{margin:0;background:var(--bg);color:var(--text);font-family:system-ui,sans-serif}
a{color:#9bd;text-decoration:none}
.container{max-width:1100px;margin:0 auto;padding:24px}
.card{background:var(--card);border-radius:16px;padding:18px;margin:12px 0;box-shadow:0 8px 24px rgba(0,0,0,.25)}
.row{display:grid;grid-template-columns:1fr 1fr;gap:12px}
input,select,button,textarea{background:#0f1728;color:var(--text);border:1px solid #23325a;border-radius:10px;padding:10px}
button{cursor:pointer}
.badge{display:inline-flex;align-items:center;gap:8px;padding:6px 10px;border-radius:999px;font-weight:600}

=== FILE: apps/portal-next/app/page.tsx ===
"use client";
import { useEffect, useState } from "react";
import { AtlasClient } from "@atlasbank/sdk";

const sdk = new AtlasClient({
  baseUrl: process.env.NEXT_PUBLIC_PAYMENTS_BASE || "http://localhost:5191",
  tenantId: process.env.NEXT_PUBLIC_TENANT || "tnt_demo",
  limitsBase: process.env.NEXT_PUBLIC_LIMITS_BASE || "http://localhost:5901",
  trustBadgeBase: process.env.NEXT_PUBLIC_TRUST_PORTAL || "http://localhost:5802"
});

export default function Home() {
  return (
    <div className="container">
      <h1>Atlas Portal</h1>
      <p className="muted">Ops & Merchant control surface (fast-path safe).</p>
      <div className="card"><TransferCard /></div>
      <div className="card"><LimitsCard /></div>
      <div className="card"><TrustCard /></div>
    </div>
  );
}

function TransferCard() {
  const [src, setSrc] = useState("msisdn::2348100000001");
  const [dst, setDst] = useState("msisdn::2348100000002");
  const [minor, setMinor] = useState("25000");
  const [out, setOut] = useState<any>(null);
  const submit = async () => {
    try {
      const res = await sdk.transferWithRisk({
        SourceAccountId: src, DestinationAccountId: dst,
        Minor: Number(minor), Currency: "NGN", Narration: "portal"
      });
      setOut(res);
    } catch (e: any) { setOut({ error: e.message }); }
  };
  return (
    <>
      <h3>Instant Transfer (with risk/limits)</h3>
      <div className="row">
        <input value={src} onChange={e=>setSrc(e.target.value)} placeholder="Source accountId" />
        <input value={dst} onChange={e=>setDst(e.target.value)} placeholder="Destination accountId" />
      </div>
      <div className="row">
        <input value={minor} onChange={e=>setMinor(e.target.value)} placeholder="Amount (minor)" />
        <button onClick={submit}>Send</button>
      </div>
      <pre style={{whiteSpace:"pre-wrap"}}>{out ? JSON.stringify(out,null,2) : ""}</pre>
    </>
  );
}

function LimitsCard() {
  const [json, setJson] = useState<string>("");
  const [msg, setMsg] = useState<string>("");
  useEffect(() => { fetch((process.env.NEXT_PUBLIC_LIMITS_BASE||"http://localhost:5901")+"/limits/policy")
    .then(r=>r.text()).then(setJson).catch(()=>{}); }, []);
  const save = async () => {
    try {
      const res = await fetch((process.env.NEXT_PUBLIC_LIMITS_BASE||"http://localhost:5901")+"/limits/policy", {
        method:"POST", headers:{"Content-Type":"application/json"}, body: json
      });
      if (!res.ok) throw new Error(await res.text());
      setMsg("Saved ✓");
    } catch (e:any) { setMsg("Error: "+e.message); }
  };
  return (
    <>
      <h3>Limits Policy (JSON)</h3>
      <textarea style={{width:"100%",minHeight:200}} value={json} onChange={e=>setJson(e.target.value)} />
      <div style={{display:"flex",gap:8,marginTop:8}}>
        <button onClick={save}>Save Policy</button>
        <span>{msg}</span>
      </div>
    </>
  );
}

function TrustCard() {
  const [entity, setEntity] = useState("m-123");
  const badgeUrl = `${process.env.NEXT_PUBLIC_TRUST_PORTAL || "http://localhost:5802"}/badge/${encodeURIComponent(entity)}.svg`;
  return (
    <>
      <h3>Trust Badge</h3>
      <div className="row">
        <input value={entity} onChange={e=>setEntity(e.target.value)} placeholder="Entity ID"/>
        <img src={badgeUrl} alt="trust badge" height={40} style={{background:"#0b1220",borderRadius:8}} />
      </div>
      <small>Public badge comes from the Trust Portal (Phase 18).</small>
    </>
  );
}

=== FILE: apps/portal-next/README.md ===
# Atlas Portal (Next.js)
- Transfer funds using the **fast path** (`/payments/transfers/with-risk`)
- Edit **Limits policy** (Phase 19)
- Show **Trust badge** (Phase 18)

## Dev
