import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { CallToolRequestSchema, ListToolsRequestSchema, ErrorCode, McpError, } from "@modelcontextprotocol/sdk/types.js";
import axios from "axios";
import express from "express";
import { randomUUID } from "crypto";
// Configuration
const MERCURIUS_BASE_URL = process.env.MERCURIUS_BASE_URL || "http://localhost:5094";
const MERCURIUS_API_KEY = process.env.MERCURIUS_API_KEY || "local-dev-only-key-not-a-secret";
const ADMIN_EMAIL = process.env.MERCURIUS_ADMIN_EMAIL || "admin@mercurius.local";
const ADMIN_PASSWORD = process.env.MERCURIUS_ADMIN_PASSWORD || "Admin123!";
// Mercurius API Client
class MercuriusClient {
    client;
    config;
    tokens = {};
    constructor(config) {
        this.config = config;
        this.client = axios.create({
            baseURL: config.baseUrl,
            withCredentials: true,
            headers: {
                "Content-Type": "application/json",
            },
        });
        // Add request interceptor for auth
        this.client.interceptors.request.use((config) => {
            if (this.tokens.accessToken) {
                config.headers.Authorization = `Bearer ${this.tokens.accessToken}`;
            }
            if (this.tokens.cookieJar) {
                config.headers.Cookie = this.tokens.cookieJar.join("; ");
            }
            return config;
        });
        // Add response interceptor to capture cookies
        this.client.interceptors.response.use((response) => {
            const setCookie = response.headers["set-cookie"];
            if (setCookie) {
                this.tokens.cookieJar = setCookie.map((c) => c.split(";")[0]);
            }
            return response;
        });
    }
    async login() {
        try {
            // First get the login page to get antiforgery token
            const loginPage = await this.client.get("/Identity/Account/Login");
            const tokenMatch = loginPage.data.match(/name="__RequestVerificationToken" value="([^"]+)"/);
            const antiforgeryToken = tokenMatch ? tokenMatch[1] : "";
            // Login
            const formData = new URLSearchParams();
            formData.append("Input.Email", this.config.adminEmail);
            formData.append("Input.Password", this.config.adminPassword);
            formData.append("__RequestVerificationToken", antiforgeryToken);
            formData.append("Input.RememberMe", "true");
            await this.client.post("/Identity/Account/Login", formData, {
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded",
                    "RequestVerificationToken": antiforgeryToken,
                },
            });
            console.error("Logged in successfully");
        }
        catch (error) {
            console.error("Login failed:", error);
            throw new McpError(ErrorCode.InternalError, "Failed to login to Mercurius");
        }
    }
    async ensureAuthenticated() {
        if (!this.tokens.cookieJar || this.tokens.cookieJar.length === 0) {
            await this.login();
        }
    }
    // Products
    async listProducts(params = {}) {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/products", { params });
        return response.data;
    }
    async getProduct(id) {
        await this.ensureAuthenticated();
        const response = await this.client.get(`/api/products/${id}`);
        return response.data;
    }
    async createProduct(product) {
        await this.ensureAuthenticated();
        const response = await this.client.post("/api/products", product);
        return response.data;
    }
    async updateProduct(id, product) {
        await this.ensureAuthenticated();
        const response = await this.client.put(`/api/products/${id}`, product);
        return response.data;
    }
    async deleteProduct(id) {
        await this.ensureAuthenticated();
        const response = await this.client.delete(`/api/products/${id}`);
        return response.data;
    }
    // Categories
    async listCategories() {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/productcategories");
        return response.data;
    }
    async createCategory(category) {
        await this.ensureAuthenticated();
        const response = await this.client.post("/api/productcategories", category);
        return response.data;
    }
    // Sales/POS
    async createSale(sale) {
        await this.ensureAuthenticated();
        const response = await this.client.post("/api/sales", sale);
        return response.data;
    }
    async getSale(id) {
        await this.ensureAuthenticated();
        const response = await this.client.get(`/api/sales/${id}`);
        return response.data;
    }
    async listSales(params = {}) {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/sales", { params });
        return response.data;
    }
    // Customers
    async listCustomers(params = {}) {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/customers", { params });
        return response.data;
    }
    async getCustomer(id) {
        await this.ensureAuthenticated();
        const response = await this.client.get(`/api/customers/${id}`);
        return response.data;
    }
    async createCustomer(customer) {
        await this.ensureAuthenticated();
        const response = await this.client.post("/api/customers", customer);
        return response.data;
    }
    async updateCustomer(id, customer) {
        await this.ensureAuthenticated();
        const response = await this.client.put(`/api/customers/${id}`, customer);
        return response.data;
    }
    // Suppliers
    async listSuppliers(params = {}) {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/suppliers", { params });
        return response.data;
    }
    async createSupplier(supplier) {
        await this.ensureAuthenticated();
        const response = await this.client.post("/api/suppliers", supplier);
        return response.data;
    }
    // Purchase Orders
    async listPurchaseOrders(params = {}) {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/purchaseorders", { params });
        return response.data;
    }
    async createPurchaseOrder(po) {
        await this.ensureAuthenticated();
        const response = await this.client.post("/api/purchaseorders", po);
        return response.data;
    }
    // Shipments
    async listShipments(params = {}) {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/shipments", { params });
        return response.data;
    }
    async createShipment(shipment) {
        await this.ensureAuthenticated();
        const response = await this.client.post("/api/shipments", shipment);
        return response.data;
    }
    // Adjustments
    async listAdjustments(params = {}) {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/adjustments", { params });
        return response.data;
    }
    async createAdjustment(adjustment) {
        await this.ensureAuthenticated();
        const response = await this.client.post("/api/adjustments", adjustment);
        return response.data;
    }
    async listAdjustmentReasons() {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/adjustmentreasons");
        return response.data;
    }
    // Invoices
    async listInvoices(params = {}) {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/invoices", { params });
        return response.data;
    }
    async getInvoice(id) {
        await this.ensureAuthenticated();
        const response = await this.client.get(`/api/invoices/${id}`);
        return response.data;
    }
    // Users (Admin)
    async listUsers(params = {}) {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/users", { params });
        return response.data;
    }
    async createUser(user) {
        await this.ensureAuthenticated();
        const response = await this.client.post("/api/users", user);
        return response.data;
    }
    // Admin Data Query (raw SQL)
    async executeSql(sql) {
        await this.ensureAuthenticated();
        // Get antiforgery token for DataQuery endpoint
        const dataQueryPage = await this.client.get("/DataQuery");
        const tokenMatch = dataQueryPage.data.match(/name="__RequestVerificationToken" value="([^"]+)"/);
        const antiforgeryToken = tokenMatch ? tokenMatch[1] : "";
        const response = await this.client.post("/DataQuery/Execute", {
            apiKey: this.config.apiKey,
            sql: sql,
        }, {
            headers: {
                "RequestVerificationToken": antiforgeryToken,
            },
        });
        return response.data;
    }
    // Dashboard
    async getDashboardStats() {
        await this.ensureAuthenticated();
        const response = await this.client.get("/api/dashboard/stats");
        return response.data;
    }
    // Health check
    async healthCheck() {
        const response = await this.client.get("/health");
        return response.data;
    }
}
// Tool definitions
const tools = [
    // Products
    {
        name: "list_products",
        description: "List products with optional search, filtering, and pagination",
        inputSchema: {
            type: "object",
            properties: {
                search: { type: "string", description: "Search by name, SKU, or barcode" },
                categoryId: { type: "number", description: "Filter by category ID" },
                page: { type: "number", default: 1, description: "Page number" },
                pageSize: { type: "number", default: 25, description: "Items per page" },
                sort: { type: "string", description: "Sort field" },
                desc: { type: "boolean", default: false, description: "Sort descending" },
            },
        },
    },
    {
        name: "get_product",
        description: "Get a single product by ID",
        inputSchema: {
            type: "object",
            properties: {
                id: { type: "number", description: "Product ID" },
            },
            required: ["id"],
        },
    },
    {
        name: "create_product",
        description: "Create a new product",
        inputSchema: {
            type: "object",
            properties: {
                name: { type: "string", description: "Product name" },
                productCode: { type: "string", description: "SKU/Product code" },
                barcode: { type: "string", description: "Barcode" },
                productCategoryId: { type: "number", description: "Category ID" },
                description: { type: "string", description: "Product description" },
                costPrice: { type: "number", description: "Cost price" },
                markupPercent: { type: "number", description: "Markup percentage" },
                salePrice: { type: "number", description: "Sale price" },
                wholesalePrice: { type: "number", description: "Wholesale price" },
                leadTimeDays: { type: "number", description: "Lead time in days" },
                lowStockCount: { type: "number", description: "Low stock threshold" },
                note: { type: "string", description: "Internal notes" },
            },
            required: ["name", "productCode"],
        },
    },
    {
        name: "update_product",
        description: "Update an existing product",
        inputSchema: {
            type: "object",
            properties: {
                id: { type: "number", description: "Product ID" },
                name: { type: "string" },
                productCode: { type: "string" },
                barcode: { type: "string" },
                productCategoryId: { type: "number" },
                description: { type: "string" },
                costPrice: { type: "number" },
                markupPercent: { type: "number" },
                salePrice: { type: "number" },
                wholesalePrice: { type: "number" },
                leadTimeDays: { type: "number" },
                lowStockCount: { type: "number" },
                note: { type: "string" },
            },
            required: ["id"],
        },
    },
    {
        name: "delete_product",
        description: "Delete a product",
        inputSchema: {
            type: "object",
            properties: {
                id: { type: "number", description: "Product ID" },
            },
            required: ["id"],
        },
    },
    // Categories
    {
        name: "list_categories",
        description: "List all product categories",
        inputSchema: { type: "object", properties: {} },
    },
    {
        name: "create_category",
        description: "Create a new product category",
        inputSchema: {
            type: "object",
            properties: {
                name: { type: "string", description: "Category name" },
                description: { type: "string", description: "Category description" },
            },
            required: ["name"],
        },
    },
    // Sales/POS
    {
        name: "create_sale",
        description: "Create a new sale (POS transaction)",
        inputSchema: {
            type: "object",
            properties: {
                customerId: { type: "number", description: "Customer ID (optional)" },
                lines: {
                    type: "array",
                    items: {
                        type: "object",
                        properties: {
                            productId: { type: "number" },
                            quantity: { type: "number" },
                            unitPrice: { type: "number" },
                            discountPercent: { type: "number" },
                        },
                        required: ["productId", "quantity", "unitPrice"],
                    },
                },
                paymentMethod: { type: "string", description: "Cash, Card, etc." },
                amountTendered: { type: "number", description: "Cash received" },
                notes: { type: "string" },
            },
            required: ["lines", "paymentMethod", "amountTendered"],
        },
    },
    {
        name: "get_sale",
        description: "Get a sale by ID",
        inputSchema: {
            type: "object",
            properties: {
                id: { type: "number", description: "Sale ID" },
            },
            required: ["id"],
        },
    },
    {
        name: "list_sales",
        description: "List sales with optional filtering",
        inputSchema: {
            type: "object",
            properties: {
                startDate: { type: "string", description: "Start date (ISO)" },
                endDate: { type: "string", description: "End date (ISO)" },
                customerId: { type: "number" },
                page: { type: "number", default: 1 },
                pageSize: { type: "number", default: 25 },
            },
        },
    },
    // Customers
    {
        name: "list_customers",
        description: "List customers with optional search and pagination",
        inputSchema: {
            type: "object",
            properties: {
                search: { type: "string" },
                page: { type: "number", default: 1 },
                pageSize: { type: "number", default: 25 },
            },
        },
    },
    {
        name: "get_customer",
        description: "Get a customer by ID",
        inputSchema: {
            type: "object",
            properties: {
                id: { type: "number", description: "Customer ID" },
            },
            required: ["id"],
        },
    },
    {
        name: "create_customer",
        description: "Create a new customer",
        inputSchema: {
            type: "object",
            properties: {
                firstName: { type: "string" },
                middleName: { type: "string" },
                lastName: { type: "string" },
                age: { type: "number" },
                gender: { type: "string" },
                contactNumber: { type: "string" },
                emailAddress: { type: "string" },
            },
            required: ["firstName", "lastName"],
        },
    },
    {
        name: "update_customer",
        description: "Update a customer",
        inputSchema: {
            type: "object",
            properties: {
                id: { type: "number" },
                firstName: { type: "string" },
                middleName: { type: "string" },
                lastName: { type: "string" },
                age: { type: "number" },
                gender: { type: "string" },
                contactNumber: { type: "string" },
                emailAddress: { type: "string" },
            },
            required: ["id"],
        },
    },
    // Suppliers
    {
        name: "list_suppliers",
        description: "List all suppliers",
        inputSchema: { type: "object", properties: {} },
    },
    {
        name: "create_supplier",
        description: "Create a new supplier",
        inputSchema: {
            type: "object",
            properties: {
                name: { type: "string" },
                contactPerson: { type: "string" },
                email: { type: "string" },
                phone: { type: "string" },
                address: { type: "string" },
            },
            required: ["name"],
        },
    },
    // Purchase Orders
    {
        name: "list_purchase_orders",
        description: "List purchase orders",
        inputSchema: {
            type: "object",
            properties: {
                status: { type: "string" },
                supplierId: { type: "number" },
                page: { type: "number", default: 1 },
                pageSize: { type: "number", default: 25 },
            },
        },
    },
    {
        name: "create_purchase_order",
        description: "Create a new purchase order",
        inputSchema: {
            type: "object",
            properties: {
                supplierId: { type: "number" },
                lines: {
                    type: "array",
                    items: {
                        type: "object",
                        properties: {
                            productId: { type: "number" },
                            quantity: { type: "number" },
                            unitCost: { type: "number" },
                        },
                        required: ["productId", "quantity", "unitCost"],
                    },
                },
                notes: { type: "string" },
            },
            required: ["supplierId", "lines"],
        },
    },
    // Shipments
    {
        name: "list_shipments",
        description: "List shipments",
        inputSchema: {
            type: "object",
            properties: {
                status: { type: "string" },
                supplierId: { type: "number" },
                purchaseOrderId: { type: "number" },
                page: { type: "number", default: 1 },
                pageSize: { type: "number", default: 25 },
            },
        },
    },
    {
        name: "create_shipment",
        description: "Create a new shipment (receive stock)",
        inputSchema: {
            type: "object",
            properties: {
                purchaseOrderId: { type: "number" },
                supplierId: { type: "number" },
                arrivalDate: { type: "string", description: "ISO date" },
                status: { type: "string", enum: ["Pending", "Received", "Delayed", "Damaged"] },
                trackingNumber: { type: "string" },
                notes: { type: "string" },
                lines: {
                    type: "array",
                    items: {
                        type: "object",
                        properties: {
                            productId: { type: "number" },
                            quantity: { type: "number" },
                            unitCost: { type: "number" },
                        },
                        required: ["productId", "quantity", "unitCost"],
                    },
                },
            },
            required: ["supplierId", "arrivalDate", "status", "lines"],
        },
    },
    // Adjustments
    {
        name: "list_adjustments",
        description: "List stock adjustments",
        inputSchema: {
            type: "object",
            properties: {
                productId: { type: "number" },
                reasonId: { type: "number" },
                page: { type: "number", default: 1 },
                pageSize: { type: "number", default: 25 },
            },
        },
    },
    {
        name: "create_adjustment",
        description: "Create a stock adjustment",
        inputSchema: {
            type: "object",
            properties: {
                productId: { type: "number" },
                adjustmentReasonId: { type: "number" },
                quantity: { type: "number" },
                note: { type: "string" },
            },
            required: ["productId", "adjustmentReasonId", "quantity"],
        },
    },
    {
        name: "list_adjustment_reasons",
        description: "List all adjustment reasons",
        inputSchema: { type: "object", properties: {} },
    },
    // Invoices
    {
        name: "list_invoices",
        description: "List invoices",
        inputSchema: {
            type: "object",
            properties: {
                search: { type: "string" },
                status: { type: "string" },
                page: { type: "number", default: 1 },
                pageSize: { type: "number", default: 25 },
            },
        },
    },
    {
        name: "get_invoice",
        description: "Get an invoice by ID",
        inputSchema: {
            type: "object",
            properties: {
                id: { type: "number", description: "Invoice ID" },
            },
            required: ["id"],
        },
    },
    // Users (Admin)
    {
        name: "list_users",
        description: "List users (admin)",
        inputSchema: {
            type: "object",
            properties: {
                search: { type: "string" },
                page: { type: "number", default: 1 },
                pageSize: { type: "number", default: 25 },
            },
        },
    },
    {
        name: "create_user",
        description: "Create a new user (admin)",
        inputSchema: {
            type: "object",
            properties: {
                firstName: { type: "string" },
                lastName: { type: "string" },
                email: { type: "string" },
                password: { type: "string" },
                roles: { type: "array", items: { type: "string" } },
            },
            required: ["firstName", "lastName", "email", "password"],
        },
    },
    // Admin Data Query
    {
        name: "execute_sql",
        description: "Execute raw SQL query (admin only - requires AdminQueryApiKey)",
        inputSchema: {
            type: "object",
            properties: {
                sql: { type: "string", description: "SQL query to execute" },
            },
            required: ["sql"],
        },
    },
    // Dashboard
    {
        name: "get_dashboard_stats",
        description: "Get dashboard statistics",
        inputSchema: { type: "object", properties: {} },
    },
    // Health
    {
        name: "health_check",
        description: "Check if Mercurius app is healthy",
        inputSchema: { type: "object", properties: {} },
    },
];
// Create MCP server
const server = new Server({
    name: "mercurius-mcp",
    version: "1.0.0",
}, {
    capabilities: {
        tools: {},
    },
});
// Initialize client
const client = new MercuriusClient({
    baseUrl: MERCURIUS_BASE_URL,
    apiKey: MERCURIUS_API_KEY,
    adminEmail: ADMIN_EMAIL,
    adminPassword: ADMIN_PASSWORD,
});
// List tools handler
server.setRequestHandler(ListToolsRequestSchema, async () => {
    return { tools };
});
// Call tool handler
server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;
    const a = args ?? {};
    try {
        let result;
        switch (name) {
            // Products
            case "list_products":
                result = await client.listProducts(a);
                break;
            case "get_product":
                result = await client.getProduct(Number(a.id));
                break;
            case "create_product":
                result = await client.createProduct(a);
                break;
            case "update_product":
                result = await client.updateProduct(Number(a.id), a);
                break;
            case "delete_product":
                result = await client.deleteProduct(Number(a.id));
                break;
            // Categories
            case "list_categories":
                result = await client.listCategories();
                break;
            case "create_category":
                result = await client.createCategory(a);
                break;
            // Sales
            case "create_sale":
                result = await client.createSale(a);
                break;
            case "get_sale":
                result = await client.getSale(Number(a.id));
                break;
            case "list_sales":
                result = await client.listSales(a);
                break;
            // Customers
            case "list_customers":
                result = await client.listCustomers(a);
                break;
            case "get_customer":
                result = await client.getCustomer(Number(a.id));
                break;
            case "create_customer":
                result = await client.createCustomer(a);
                break;
            case "update_customer":
                result = await client.updateCustomer(Number(a.id), a);
                break;
            // Suppliers
            case "list_suppliers":
                result = await client.listSuppliers();
                break;
            case "create_supplier":
                result = await client.createSupplier(a);
                break;
            // Purchase Orders
            case "list_purchase_orders":
                result = await client.listPurchaseOrders(a);
                break;
            case "create_purchase_order":
                result = await client.createPurchaseOrder(a);
                break;
            // Shipments
            case "list_shipments":
                result = await client.listShipments(a);
                break;
            case "create_shipment":
                result = await client.createShipment(a);
                break;
            // Adjustments
            case "list_adjustments":
                result = await client.listAdjustments(a);
                break;
            case "create_adjustment":
                result = await client.createAdjustment(a);
                break;
            case "list_adjustment_reasons":
                result = await client.listAdjustmentReasons();
                break;
            // Invoices
            case "list_invoices":
                result = await client.listInvoices(a);
                break;
            case "get_invoice":
                result = await client.getInvoice(Number(a.id));
                break;
            // Users
            case "list_users":
                result = await client.listUsers(a);
                break;
            case "create_user":
                result = await client.createUser(a);
                break;
            // Admin Data Query
            case "execute_sql":
                result = await client.executeSql(String(a.sql));
                break;
            // Dashboard
            case "get_dashboard_stats":
                result = await client.getDashboardStats();
                break;
            // Health
            case "health_check":
                result = await client.healthCheck();
                break;
            default:
                throw new McpError(ErrorCode.MethodNotFound, `Unknown tool: ${name}`);
        }
        return {
            content: [
                {
                    type: "text",
                    text: JSON.stringify(result, null, 2),
                },
            ],
        };
    }
    catch (error) {
        console.error(`Tool ${name} error:`, error);
        if (error instanceof McpError)
            throw error;
        throw new McpError(ErrorCode.InternalError, error.message || "Tool execution failed");
    }
});
// HTTP/SSE Server setup
const HTTP_PORT = parseInt(process.env.MCP_HTTP_PORT || "3001", 10);
const MCP_API_KEY = process.env.MCP_API_KEY || ""; // Set to enable auth
const app = express();
app.use(express.json());
// Auth middleware for HTTP transport
function authMiddleware(req, res, next) {
    if (MCP_API_KEY) {
        const authHeader = req.headers.authorization;
        if (!authHeader || !authHeader.startsWith("Bearer ")) {
            return res.status(401).json({ error: "Missing or invalid Authorization header" });
        }
        const token = authHeader.substring(7);
        if (token !== MCP_API_KEY) {
            return res.status(401).json({ error: "Invalid API key" });
        }
    }
    next();
}
// Store transports by session ID
const transports = new Map();
// MCP endpoint for HTTP/SSE
app.post("/mcp", authMiddleware, async (req, res) => {
    const sessionId = req.headers["mcp-session-id"] || randomUUID();
    let transport = transports.get(sessionId);
    if (!transport) {
        transport = new StreamableHTTPServerTransport({
            sessionIdGenerator: () => sessionId,
            onsessioninitialized: (sid) => {
                transports.set(sid, transport);
            },
        });
        // Clean up on close
        transport.onclose = () => {
            transports.delete(sessionId);
        };
        await server.connect(transport);
    }
    await transport.handleRequest(req, res, req.body);
});
// GET for SSE streaming
app.get("/mcp", authMiddleware, async (req, res) => {
    const sessionId = req.headers["mcp-session-id"];
    if (!sessionId || !transports.has(sessionId)) {
        return res.status(404).json({ error: "Session not found" });
    }
    const transport = transports.get(sessionId);
    await transport.handleRequest(req, res);
});
// DELETE to terminate session
app.delete("/mcp", authMiddleware, async (req, res) => {
    const sessionId = req.headers["mcp-session-id"];
    if (!sessionId || !transports.has(sessionId)) {
        return res.status(404).json({ error: "Session not found" });
    }
    const transport = transports.get(sessionId);
    await transport.close();
    transports.delete(sessionId);
    res.status(204).send();
});
// Health endpoint
app.get("/health", (req, res) => {
    res.json({ status: "ok", service: "mercurius-mcp" });
});
// Start server
async function main() {
    console.error("Starting Mercurius MCP server...");
    console.error(`Connecting to Mercurius at ${MERCURIUS_BASE_URL}`);
    // Test connection
    try {
        await client.healthCheck();
        console.error("Mercurius health check passed");
    }
    catch (error) {
        console.error("Warning: Mercurius health check failed, will retry on first tool call");
    }
    const transportMode = process.env.MCP_TRANSPORT || "stdio";
    if (transportMode === "http" || transportMode === "both") {
        // Start HTTP server
        app.listen(HTTP_PORT, () => {
            console.error(`Mercurius MCP HTTP server running on http://localhost:${HTTP_PORT}/mcp`);
            if (MCP_API_KEY) {
                console.error("Authentication enabled - use Authorization: Bearer <key>");
            }
        });
    }
    if (transportMode === "stdio" || transportMode === "both") {
        // Also run stdio for local connections
        const stdioTransport = new StdioServerTransport();
        await server.connect(stdioTransport);
        console.error("Mercurius MCP server running on stdio");
    }
}
main().catch((error) => {
    console.error("Fatal error:", error);
    process.exit(1);
});
//# sourceMappingURL=index.js.map