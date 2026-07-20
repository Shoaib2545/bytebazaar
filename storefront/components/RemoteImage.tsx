import Image from "next/image";
import { canOptimize } from "@/lib/images";

interface Props {
  src: string;
  alt: string;
  /** Tailwind classes applied to the <img> itself. */
  className?: string;
  /**
   * Responsive size hint. Required for good `srcset` selection — every caller
   * renders into a fluid container, so we always fill the parent.
   */
  sizes: string;
  /** Set on the LCP candidate only (hero slide 1, product gallery main). */
  eager?: boolean;
  quality?: number;
}

/**
 * `next/image` in `fill` mode for catalog imagery of unknown intrinsic size.
 *
 * The parent element must be `relative` and have its own reserved box (an
 * aspect-ratio or a fixed height) — that is what keeps CLS at zero, since the
 * image never contributes layout of its own.
 *
 * Hosts outside NEXT_PUBLIC_IMAGE_HOSTS fall back to `unoptimized` rather than
 * erroring out of the optimizer.
 */
export default function RemoteImage({
  src,
  alt,
  className = "",
  sizes,
  eager = false,
  quality,
}: Props) {
  return (
    <Image
      src={src}
      alt={alt}
      fill
      sizes={sizes}
      className={className}
      quality={quality}
      unoptimized={!canOptimize(src)}
      // Next 16 deprecated `priority` in favour of explicit loading /
      // fetchPriority / preload. The docs recommend loading+fetchPriority over
      // `preload` in most cases, and warn against combining them.
      loading={eager ? "eager" : "lazy"}
      fetchPriority={eager ? "high" : "auto"}
    />
  );
}
