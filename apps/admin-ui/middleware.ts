import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

export function middleware(request: NextRequest) {
    const { pathname } = request.nextUrl;

    // Define public paths that don't need auth
    const isPublicPath = pathname === "/login" || pathname === "/health";

    // Check for auth token
    const hasToken = request.cookies.has("auth_token");

    // OLD LOGIN ROUTES - Redirect to new /login
    if (pathname === "/platform/login" || pathname === "/tenant/login") {
        return NextResponse.redirect(new URL("/login", request.url), 301);
    }

    // Control Center (/) - Redirect to login
    if (pathname === "/") {
        return NextResponse.redirect(new URL("/login", request.url), 301);
    }

    // PROTECTED ROUTES - Require authentication
    if (!isPublicPath) {
        if (!hasToken) {
            // All protected routes redirect to /login
            return NextResponse.redirect(new URL("/login", request.url));
        }
    }

    // ALREADY LOGGED IN - Redirect from /login to appropriate dashboard
    // Note: We can't determine role from cookie alone, so client-side will handle this
    // But we can still redirect to prevent showing login page when already logged in
    if (hasToken && pathname === "/login") {
        // Client-side will handle the redirect based on localStorage/role
        // For now, we let it through but client will redirect
        return NextResponse.next();
    }

    // Redirect /tenant to /tenant/select (if authenticated)
    if (hasToken && pathname === "/tenant") {
        return NextResponse.redirect(new URL("/tenant/select", request.url));
    }

    // Prevent browser back navigation to protected pages after logout
    // Add cache-control headers
    const response = NextResponse.next();
    if (!isPublicPath && hasToken) {
        response.headers.set("Cache-Control", "no-store, no-cache, must-revalidate, proxy-revalidate");
        response.headers.set("Pragma", "no-cache");
        response.headers.set("Expires", "0");
    }

    return response;
}

export const config = {
    matcher: [
        /*
         * Match all request paths except for the ones starting with:
         * - api (API routes)
         * - _next/static (static files)
         * - _next/image (image optimization files)
         * - favicon.ico (favicon file)
         * - images (public images)
         */
        '/((?!api|_next/static|_next/image|favicon.ico|images).*)',
    ],
};
