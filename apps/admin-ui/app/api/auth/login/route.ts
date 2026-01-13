import { cookies } from "next/headers";
import { NextResponse } from "next/server";

/**
 * Login API Route
 * 
 * Clean Architecture: API Route acts as a boundary between client and backend.
 * - Reads configuration from environment variables (not from client)
 * - Validates request payload
 * - Proxies to backend service
 * - Sets secure HttpOnly cookie
 * - Returns standardized response
 */
export async function POST(request: Request) {
    try {
        const body = await request.json();
        const { tenantId, email, password } = body;

        // Validate required fields
        if (!email || !password) {
            return NextResponse.json(
                { error: "invalid_payload", message: "E-posta ve şifre gerekli" },
                { status: 400 }
            );
        }

        // Get backend URL from environment (never trust client)
        const backendUrl = process.env.NEXT_PUBLIC_ADMIN_API_BASE_URL || "http://localhost:5114";
        const baseUrl = backendUrl.endsWith("/") ? backendUrl.slice(0, -1) : backendUrl;

        // Build request payload (only send tenantId if explicitly provided from tenant select)
        const loginPayload: { email: string; password: string; tenantId?: string } = {
            email: email.trim(),
            password,
        };

        // Only include tenantId if provided (from tenant select flow)
        if (tenantId && tenantId.trim()) {
            loginPayload.tenantId = tenantId.trim();
        }

        // Call backend login endpoint
        const response = await fetch(`${baseUrl}/auth/login`, {
            method: "POST",
            headers: { 
                "Content-Type": "application/json",
            },
            body: JSON.stringify(loginPayload),
        });

        const data = await response.json();

        if (!response.ok) {
            // Return backend error as-is (with proper status code)
            return NextResponse.json(
                { 
                    error: data.error || "invalid_credentials",
                    message: data.message || "Giriş başarısız"
                },
                { status: response.status }
            );
        }

        // Set HttpOnly Cookie only if token is present
        // (Multi-tenant login might return tenants array without token)
        if (data.token) {
            (await cookies()).set({
                name: "auth_token",
                value: data.token,
                httpOnly: true,
                path: "/",
                secure: process.env.NODE_ENV === "production",
                maxAge: 60 * 60 * 24 * 7, // 1 week
                sameSite: "lax",
            });
        }

        // Return backend response (token, user, tenants, etc.)
        return NextResponse.json(data);
    } catch (error) {
        console.error("Login API error:", error);
        return NextResponse.json(
            { error: "server_error", message: "Sunucu hatası" },
            { status: 500 }
        );
    }
}
