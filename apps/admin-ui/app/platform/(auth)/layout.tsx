import { Providers } from "@/app/providers";

export default function PlatformAuthLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <Providers scope="platform">
      <div className="min-h-screen px-6 py-12 md:px-12">
        <div className="mx-auto flex w-full max-w-4xl flex-col gap-10">
          {children}
        </div>
      </div>
    </Providers>
  );
}
