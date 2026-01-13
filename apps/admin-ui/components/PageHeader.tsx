import { ReactNode } from "react";

export function PageHeader({
  title,
  subtitle,
  action,
}: {
  title: string;
  subtitle?: string;
  action?: ReactNode;
}) {
  return (
    <div className="mb-8 flex flex-wrap items-end justify-between gap-6">
      <div className="space-y-2">
        <p className="label">Embeddra</p>
        <h1 className="font-display text-3xl text-[var(--ink)] md:text-4xl">{title}</h1>
        {subtitle && (
          <p className="max-w-2xl text-sm leading-relaxed text-[var(--muted)] md:text-base">
            {subtitle}
          </p>
        )}
      </div>
      {action}
    </div>
  );
}
