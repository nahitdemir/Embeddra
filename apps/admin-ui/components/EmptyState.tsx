export function EmptyState({
  title,
  description,
  action,
}: {
  title: string;
  description: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="card-soft flex flex-col gap-4 p-6 text-sm text-[var(--muted)]">
      <div className="flex flex-col gap-2">
        <p className="font-display text-lg text-[var(--ink)]">{title}</p>
        <p className="leading-relaxed">{description}</p>
      </div>
      {action && <div>{action}</div>}
    </div>
  );
}
