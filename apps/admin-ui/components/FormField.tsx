"use client";

import { ReactNode } from "react";
import { HelperText } from "./HelperText";

type FormFieldProps = {
  label: string;
  required?: boolean;
  error?: string;
  helperText?: string;
  children: ReactNode;
  className?: string;
};

export function FormField({
  label,
  required = false,
  error,
  helperText,
  children,
  className,
}: FormFieldProps) {
  return (
    <div className={className}>
      <label className="label flex items-center gap-1.5">
        {label}
        {required && (
          <span className="text-rose-500" aria-label="Zorunlu alan">
            *
          </span>
        )}
      </label>
      {children}
      {error && <HelperText variant="error">{error}</HelperText>}
      {!error && helperText && <HelperText>{helperText}</HelperText>}
    </div>
  );
}
