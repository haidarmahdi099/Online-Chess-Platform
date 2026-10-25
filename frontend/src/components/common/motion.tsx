import { motion, useReducedMotion, type Variants } from "motion/react";
import type { ReactNode } from "react";

/**
 * Small, shared animation building blocks.
 *
 * Rules every one of these follows:
 *  - Only `opacity` and `transform` are animated (cheap on phones).
 *  - They never wrap or intercept the chess board; gameplay stays instant.
 *  - With "reduce motion" on, content simply appears.
 */

const EASE = [0.2, 0.8, 0.2, 1] as const;

const itemVariants: Variants = {
  hidden: { opacity: 0, y: 18 },
  show: { opacity: 1, y: 0, transition: { duration: 0.5, ease: EASE } },
};

/** Fades and lifts its content in when it scrolls into view (once). */
export function Reveal({
  children,
  delay = 0,
  className,
}: {
  children: ReactNode;
  delay?: number;
  className?: string;
}) {
  const reduce = useReducedMotion();
  if (reduce) return <div className={className}>{children}</div>;
  return (
    <motion.div
      className={className}
      initial={{ opacity: 0, y: 24 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "0px 0px -60px 0px" }}
      transition={{ duration: 0.55, ease: EASE, delay }}
    >
      {children}
    </motion.div>
  );
}

/**
 * A group whose <StaggerItem> children enter one after another.
 * `inView` waits for scrolling; otherwise it plays on mount (hero, lists that just loaded).
 */
export function Stagger({
  children,
  className,
  inView = false,
  gap = 0.07,
}: {
  children: ReactNode;
  className?: string;
  inView?: boolean;
  gap?: number;
}) {
  const variants: Variants = {
    hidden: {},
    show: { transition: { staggerChildren: gap, delayChildren: 0.05 } },
  };
  const common = { className, variants, initial: "hidden" as const };
  return inView ? (
    <motion.div
      {...common}
      whileInView="show"
      viewport={{ once: true, margin: "0px 0px -60px 0px" }}
    >
      {children}
    </motion.div>
  ) : (
    <motion.div {...common} animate="show">
      {children}
    </motion.div>
  );
}

export function StaggerItem({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <motion.div className={className} variants={itemVariants}>
      {children}
    </motion.div>
  );
}

/** Soft fade/slide used for each route's content. Enter-only so navigation never waits. */
export function PageTransition({ children, id }: { children: ReactNode; id: string }) {
  return (
    <motion.div
      key={id}
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.28, ease: EASE }}
    >
      {children}
    </motion.div>
  );
}

/** A tappable card/button wrapper: gentle lift on hover, press-in on tap. */
export const tapProps = {
  whileHover: { y: -3 },
  whileTap: { scale: 0.97 },
  transition: { type: "spring", stiffness: 400, damping: 26 },
} as const;
