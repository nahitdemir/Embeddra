import { cookies } from "next/headers";
import { NextResponse } from "next/server";

export async function POST() {
    const cookieStore = await cookies();
    
    // Clear auth token cookie
    cookieStore.delete("auth_token");
    
    // Also clear any other auth-related cookies if they exist
    cookieStore.delete("auth_role");
    cookieStore.delete("auth_tenant_id");
    
    const response = NextResponse.json({ success: true });
    
    // Set cache-control headers to prevent browser back navigation
    response.headers.set("Cache-Control", "no-store, no-cache, must-revalidate, proxy-revalidate");
    response.headers.set("Pragma", "no-cache");
    response.headers.set("Expires", "0");
    
    return response;
}
