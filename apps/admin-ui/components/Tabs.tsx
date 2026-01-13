"use client";

import { cn } from "@/lib/utils";

type Tab = {
  id: string;
  label: string;
  icon?: string;
};

type TabsProps = {
  tabs: Tab[];
  activeTab: string;
  onTabChange: (tabId: string) => void;
  className?: string;
};

export function Tabs({ tabs, activeTab, onTabChange, className }: TabsProps) {
  return (
    <div className={cn("border-b border-[var(--border)]", className)}>
      <nav className="-mb-px flex gap-1" aria-label="Tabs">
        {tabs.map((tab) => {
          const isActive = tab.id === activeTab;
          return (
            <button
              key={tab.id}
              onClick={() => onTabChange(tab.id)}
              className={cn(
                "relative px-4 py-3 text-sm font-medium transition-all",
                "border-b-2 border-transparent",
                "hover:text-[var(--ink)] hover:border-[var(--border)]",
                isActive
                  ? "border-[var(--accent)] text-[var(--ink)]"
                  : "text-[var(--muted)]"
              )}
              aria-current={isActive ? "page" : undefined}
            >
              {tab.label}
              {isActive && (
                <span className="absolute bottom-0 left-0 right-0 h-0.5 bg-[var(--accent)]" />
              )}
            </button>
          );
        })}
      </nav>
    </div>
  );
}
