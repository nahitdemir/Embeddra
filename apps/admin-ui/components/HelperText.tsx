"use client";

import { cn } from "@/lib/utils";

type HelperTextProps = {
  children: React.ReactNode;
  variant?: "default" | "error" | "success";
  className?: string;
};

export function HelperText({
  children,
  variant = "default",
  className,
}: HelperTextProps) {
  return (
    <p
      className={cn(
        "mt-1.5 text-xs",
        variant === "error" && "text-rose-600",
        variant === "success" && "text-emerald-600",
        variant === "default" && "text-[var(--muted)]",
        className
      )}
    >
      {children}
    </p>
  );
}
