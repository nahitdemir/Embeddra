import { Providers } from "@/app/providers";
import { MobileNav } from "@/components/MobileNav";
import { RequireAuth } from "@/components/RequireAuth";
import { Sidebar } from "@/components/Sidebar";
import { Topbar } from "@/components/Topbar";
import { platformNavItems } from "@/lib/nav-items";

export default function PlatformPanelLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <Providers scope="platform">
      <RequireAuth panel="platform">
        <div className="app-shell">
          <Sidebar panel="platform" items={platformNavItems} />
          <div className="flex min-h-screen flex-1 flex-col">
            <Topbar panel="platform" />
            <main className="flex-1 px-6 pb-14 pt-6 md:px-10">
              {children}
            </main>
          </div>
        </div>
        <MobileNav items={platformNavItems} />
      </RequireAuth>
    </Providers>
  );
}
