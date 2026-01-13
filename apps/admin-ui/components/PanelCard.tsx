import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

type PanelCardProps = {
  label: string;
  title: string;
  description: string;
  meta?: ReactNode;
  children: ReactNode;
  className?: string;
};

export function PanelCard({
  label,
  title,
  description,
  meta,
  children,
  className,
}: PanelCardProps) {
  return (
    <section
      className={cn(
        "group relative flex h-full cursor-pointer flex-col justify-between rounded-2xl border border-[var(--border)] bg-[var(--surface)] px-6 py-6 shadow-[var(--shadow)] transition-all duration-200 ease-out hover:-translate-y-1 hover:border-[color:var(--accent)] hover:shadow-[var(--shadow-strong)] md:px-7 md:py-7",
        className
      )}
    >
      <div className="space-y-3">
        <span className="text-[11px] font-semibold uppercase tracking-[0.32em] text-[var(--muted)]">
          {label}
        </span>
        <h2 className="font-display text-2xl text-[var(--ink)] md:text-3xl">
          {title}
        </h2>
        <p className="text-sm leading-relaxed text-[var(--muted)] md:text-base">
          {description}
        </p>
        {meta && <div className="pt-1">{meta}</div>}
      </div>
      <div className="mt-6 flex flex-col gap-3 sm:flex-row sm:items-center">
        {children}
      </div>
    </section>
  );
}
