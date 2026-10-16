# AcxiomCRM Enterprise — Project Documentation & Guide

AcxiomCRM is a full-featured, enterprise-grade, role-based CRM web application developed according to **Acxiom Consulting's Project Specification and Technical Documentation**.

Built on **ASP.NET Core 8.0 MVC**, **Entity Framework Core**, **ASP.NET Core Identity**, **Bootstrap 5**, and **Chart.js**, the application covers the entire customer lifecycle from prospective lead capture through qualification, follow-ups, pipeline management, deal closing, auditability, reporting, and secured REST APIs.

---

## 1. Quick Start & Execution

### Prerequisites
- **.NET 8.0 SDK** (Installed at `C:\dotnet\dotnet.exe` or standard system path)

### Running the Web Application
Open your terminal in the solution directory and execute:
```powershell
C:\dotnet\dotnet.exe run --project AcxiomCRM/AcxiomCRM.csproj
```
The application will start, automatically create and seed the SQLite database (`acxiomcrm.db`), and listen on:
- **Web Portal**: [http://localhost:5000](http://localhost:5000) or displayed terminal URL
- **Swagger REST API UI**: [http://localhost:5000/swagger](http://localhost:5000/swagger)

### Running Automated Test Suite
The project includes automated xUnit tests validating all business logic, validation rules, and authorization boundaries:
```powershell
C:\dotnet\dotnet.exe test
```

---

## 2. Pre-Seeded Users & Role Hierarchy

AcxiomCRM implements a 3-tier Role-Based Access Control (RBAC) model as mandated by Acxiom Consulting:

| Role | Seeded Email | Password | Access Scope & Responsibilities |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@acxiom.com` | `Admin@123` | **Full Enterprise Control**: User & Role administration, lockout clearing, password reset, tamper-proof security audit log, all CRM records, and global business reports. |
| **Manager** | `manager@acxiom.com` | `Manager@123` | **Sales Operations Supervision**: Team-wide pipeline management, lead assignments, all customer records, follow-up monitoring, team productivity reports, and conversion metrics. |
| **Sales Executive 1** | `sales1@acxiom.com` | `Sales@123` | **Scoped Personal Sales Portfolio**: Strictly assigned customers, assigned leads, assigned opportunities, scheduled follow-ups, and personal performance dashboard. |
| **Sales Executive 2** | `sales2@acxiom.com` | `Sales@123` | **Scoped Personal Sales Portfolio**: Strictly assigned customers, assigned leads, assigned opportunities, scheduled follow-ups, and personal performance dashboard. |

*Quick login buttons are built directly into the login screen to allow evaluators to switch between Admin, Manager, and Sales Executive seamlessly.*

---

## 3. Mandatory Module Implementation Matrix

| Module | Core Purpose & Capabilities | Specification Reference |
| :--- | :--- | :--- |
| **Dashboard** | Role-scoped KPI cards (Total Customers, Total Leads, Open Leads, Total Opportunities, Open/Won/Lost Opportunities, Total Pipeline Value, Weighted Forecast, Overdue Follow-ups). Includes interactive **Chart.js** charts (Lead Status doughnut, Pipeline Stage bar, Monthly Won trend) and quick date filtering (Today, This Week, This Month, All Time). | Section 4.3, 17.11, 17.12 |
| **Customer Management** | Master accounts with Name, Email, Phone, Company, Address, Status, and Assigned Rep. Includes uniqueness enforcement (Email & Phone), duplicate prevention, activity history, and soft deactivation protection. | Section 4.4, 17.3, 17.5 |
| **Lead Management** | Capture prospective leads with Source, Status (New, Contacted, Qualified, Unqualified, Converted, Lost), Priority, and Expected Value. Includes 1-click **Lead Conversion Workflow** to create Customers and Opportunities with audit tracking. | Section 4.5, 8, 17.6 |
| **Opportunity Management** | Deal pipeline stages (Qualification, Proposal, Negotiation, Won, Lost), Amount, Win Probability (0–100%), Expected Close Date, and derived **Weighted Pipeline** (`Amount × Probability / 100`). | Section 4.8, 17.3, 17.7 |
| **Follow-Up Management** | Schedule Calls, Meetings, Demos, and Emails against Customers, Leads, and Opportunities. Statuses: Planned, Completed, Missed, Cancelled. Features inline **Complete** and **Reschedule** action modals. | Section 4.6, 17.3, 17.7 |
| **Activity Tracking** | Log client touchpoints across Calls, Meetings, Emails, and Tasks with timestamps and representative attribution. | Section 14, 17.2, 17.3 |
| **User & Role Administration** | Admin-exclusive panel to create users, assign primary roles, toggle active status, unlock locked-out accounts, and trigger secure password resets. | Section 4.7, 7, 17.10 |
| **Audit Trail** | Append-only, immutable audit trail capturing user ID, timestamp, entity module, action (Login, Failed Login, Logout, Create, Update, Delete, Role Change, Conversion), old/new values, IP address, and details. | Section 4.9, 17.16 |
| **Reports & BI** | Stage-wise/owner-wise Pipeline report, Lead Conversion report, Follow-up report, Team Productivity report, and 1-click CSV export. | Section 4.11, 11 |
| **REST API & Swagger** | Secured RESTful endpoints exposing DTOs, proper HTTP status codes (200, 201, 400, 401, 403, 404, 409), input validation, and interactive OpenAPI Swagger UI at `/swagger`. | Section 4.10, 10, 17.14 |

---

## 4. Business Validation & Security Architecture

### Business Validation Rules Enforced
1. **Opportunity Amount**: Must be greater than 0 for active sales opportunities. Requests with `Amount <= 0` are rejected on both client and server (`Opportunity Amount must be greater than 0.`).
2. **Win Probability**: Must be between 0 and 100 inclusive. Requests outside this range are rejected (`Probability must be between 0 and 100.`).
3. **Expected Close Date**: Cannot be in the past for active opportunities (`Expected Close Date cannot be in the past.`).
4. **Follow-Up Date**: Cannot be earlier than today for new or planned follow-ups (`Follow-up date cannot be earlier than today.`).
5. **Customer Uniqueness**: Rejects creation of customer with existing Email or Phone (HTTP 409 Conflict / form error).
6. **Input Formatting**: Regular expression enforcement for phone numbers (7–15 digits) and RFC-compliant email validation.

### Security Controls (Section 6 & 14)
- **ASP.NET Core Identity**: Secure password hashing (PBKDF2 with HMAC-SHA256).
- **Password Complexity Policy**: Minimum 8 characters, requiring digit, uppercase, lowercase, and special character.
- **Account Lockout**: 5 consecutive failed login attempts trigger an automatic 15-minute lockout. Unlocking can be triggered by authorized Administrators.
- **Anti-Forgery Protection**: State-changing POST forms are protected with `[ValidateAntiForgeryToken]`.
- **SQL Injection Prevention**: Entity Framework Core parameterized query APIs used exclusively.
- **Data Protection**: Password hashes and sensitive internal fields are never exposed in ViewModels, DTOs, or logs.

---

## 5. REST API Reference

Interactive Swagger documentation is available at `/swagger`.

| Method | Endpoint | Description | Status Codes |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Authenticate user credentials & issue cookie | `200`, `400`, `401`, `423` (Locked) |
| `POST` | `/api/auth/logout` | Terminate active authenticated session | `200` |
| `GET` | `/api/customers` | Retrieve customers (scoped by caller role) | `200`, `401` |
| `GET` | `/api/customers/{id}` | Get customer details by ID | `200`, `401`, `404` |
| `POST` | `/api/customers` | Create new customer (checks email/phone uniqueness) | `201`, `400`, `409` |
| `PUT` | `/api/customers/{id}` | Update customer record | `200`, `400`, `404`, `409` |
| `DELETE`| `/api/customers/{id}` | Delete or deactivate customer | `200`, `404` |
| `GET` | `/api/leads` | List leads with optional search & status filter | `200`, `401` |
| `POST` | `/api/leads` | Create prospective lead | `201`, `400` |
| `GET` | `/api/opportunities` | List opportunities in scope | `200`, `401` |
| `POST` | `/api/opportunities` | Create opportunity (enforces amount > 0, prob 0-100) | `201`, `400` |
| `GET` | `/api/followups` | List scheduled follow-ups | `200`, `401` |
| `POST` | `/api/followups` | Create follow-up (rejects past dates) | `201`, `400` |
| `GET` | `/api/reports/pipeline`| Aggregated stage-wise and owner-wise pipeline metrics | `200`, `401` |
