import { revalidatePath } from "next/cache";
import { NextRequest, NextResponse } from "next/server";

/**
 * ISR revalidation webhook hook point.
 * POST { path: "/category/gpus", secret: "dev-secret" }
 */
export async function POST(request: NextRequest) {
  let body: { path?: unknown; secret?: unknown } = {};
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ message: "Invalid JSON body" }, { status: 400 });
  }

  const secret = process.env.REVALIDATE_SECRET || "dev-secret";
  if (body.secret !== secret) {
    return NextResponse.json({ message: "Invalid secret" }, { status: 401 });
  }

  if (typeof body.path !== "string" || !body.path.startsWith("/")) {
    return NextResponse.json(
      { message: "A 'path' starting with '/' is required" },
      { status: 400 }
    );
  }

  revalidatePath(body.path);
  return NextResponse.json({
    revalidated: true,
    path: body.path,
    now: Date.now(),
  });
}
