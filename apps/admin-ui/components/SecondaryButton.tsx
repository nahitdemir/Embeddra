import Link from "next/link";
import type { ComponentPropsWithoutRef } from "react";
import { cn } from "@/lib/utils";

type ButtonProps = ComponentPropsWithoutRef<"button"> & {
  fullWidth?: boolean;
};

export function SecondaryButton({
  className,
  fullWidth,
  type = "button",
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      className={cn("btn-outline", fullWidth && "w-full", className)}
      {...props}
    />
  );
}

type LinkProps = ComponentPropsWithoutRef<typeof Link> & {
  fullWidth?: boolean;
};

export function SecondaryButtonLink({ className, fullWidth, ...props }: LinkProps) {
  return (
    <Link className={cn("btn-outline", fullWidth && "w-full", className)} {...props} />
  );
}
