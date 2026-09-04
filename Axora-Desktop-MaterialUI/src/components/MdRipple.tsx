import React, { useState, useEffect, useRef, useCallback, ReactNode, MouseEvent } from "react";
import { motion } from "framer-motion";

interface RippleItem {
  x: number;
  y: number;
  size: number;
  id: number;
}

interface MdRippleProps {
  children: ReactNode;
  className?: string;
  style?: React.CSSProperties;
  onClick?: (e: MouseEvent<HTMLDivElement>) => void;
  color?: string;
  disabled?: boolean;
  title?: string;
  role?: string;
  tabIndex?: number;
  onKeyDown?: (e: React.KeyboardEvent<HTMLDivElement>) => void;
  "aria-label"?: string;
}

/**
 * MdRipple — Material Design 3 compliant ripple effect component.
 *
 * Wraps any element with an MD3 ripple that:
 * - Expands from the exact pointer-down location
 * - Scales to fill the full element
 * - Fades out with MD3 standard easing (cubic-bezier(0.4, 0, 0.2, 1))
 * - Duration: 600ms (MD3 long-2 slot)
 * - Accessibility: Defaults to role="button" and tabIndex=0 when onClick is provided
 */
export function MdRipple({
  children,
  className = "",
  style,
  onClick,
  color = "currentColor",
  disabled = false,
  title,
  role,
  tabIndex,
  onKeyDown,
  "aria-label": ariaLabel,
}: MdRippleProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const rippleIdRef = useRef(0);
  const mountedRef = useRef(true);
  const [ripples, setRipples] = useState<RippleItem[]>([]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const handleClick = useCallback(
    (e: MouseEvent<HTMLDivElement>) => {
      if (disabled) return;

      const container = containerRef.current;
      if (!container) return;

      const rect = container.getBoundingClientRect();
      const x = e.clientX - rect.left;
      const y = e.clientY - rect.top;

      // Ripple must cover the entire element
      const size = Math.max(rect.width, rect.height) * 2.5;
      const id = ++rippleIdRef.current;

      if (mountedRef.current) {
        setRipples((prev) => [...prev, { x, y, size, id }]);
      }

      // Cleanup after animation
      setTimeout(() => {
        if (mountedRef.current) {
          setRipples((prev) => prev.filter((r) => r.id !== id));
        }
      }, 700);

      onClick?.(e);
    },
    [disabled, onClick]
  );

  const computedRole = role || (onClick ? "button" : undefined);
  const computedTabIndex = tabIndex !== undefined ? tabIndex : (onClick && !disabled ? 0 : undefined);

  return (
    <div
      ref={containerRef}
      role={computedRole}
      tabIndex={computedTabIndex}
      aria-label={ariaLabel}
      className={`relative overflow-hidden ${disabled ? "pointer-events-none" : "cursor-pointer"} ${className}`}
      style={style}
      onClick={handleClick}
      onKeyDown={(e) => {
        if (!disabled && onClick && (e.key === "Enter" || e.key === " ")) {
          e.preventDefault();
          onClick(e as any);
        }
        onKeyDown?.(e);
      }}
      title={title}
    >
      {children}
      {ripples.map((ripple) => (
        <motion.span
          key={ripple.id}
          className="absolute rounded-full pointer-events-none"
          style={{
            left: ripple.x - ripple.size / 2,
            top: ripple.y - ripple.size / 2,
            width: ripple.size,
            height: ripple.size,
            backgroundColor: color,
          }}
          initial={{ scale: 0, opacity: 0.28 }}
          animate={{ scale: 1, opacity: 0 }}
          transition={{
            duration: 0.6,
            ease: [0.4, 0, 0.2, 1], // MD3 standard easing
          }}
        />
      ))}
    </div>
  );
}
