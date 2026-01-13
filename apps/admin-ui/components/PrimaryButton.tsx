import Link from "next/link";
import type { ComponentPropsWithoutRef } from "react";
import { cn } from "@/lib/utils";

type ButtonProps = ComponentPropsWithoutRef<"button"> & {
  fullWidth?: boolean;
};

export function PrimaryButton({
  className,
  fullWidth,
  type = "button",
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      className={cn("btn-primary", fullWidth && "w-full", className)}
      {...props}
    />
  );
}

type LinkProps = ComponentPropsWithoutRef<typeof Link> & {
  fullWidth?: boolean;
};

export function PrimaryButtonLink({ className, fullWidth, ...props }: LinkProps) {
  return (
    <Link className={cn("btn-primary", fullWidth && "w-full", className)} {...props} />
  );
}
