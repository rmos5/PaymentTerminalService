# Contributing

## Guidelines
- Keep return paths explicit. Avoid hidden or silent early returns that skip work without a clear error or documented reason.
- When input violates a method's contract, throw a clear exception instead of returning silently.

## Standards
- Prefer explicit error handling over silent no-ops when input is invalid or unexpected.
- Declare methods in dependency order when practical: if a method is used by another method in the same file, place the called method before the call site.

## Code Organization
- Prefer top-down readability for public API surfaces, but when choosing method order within an implementation file, place lower-level helpers and shared methods before the methods that call them when practical.
- Keep related lifecycle, validation, and session-management methods grouped together.

## Tracing and Diagnostics

### Trace Method Entry

Always place `Trace.WriteLine` at the **beginning** of important methods, **before** argument validation:

**Rationale**: This ensures that method entry is logged even when validation fails, improving diagnostic visibility for invalid calls.

**When to apply**:
- Public API methods
- Protected template methods
- Methods with complex state transitions
- Session/transaction lifecycle methods

**When to skip**:
- Simple property getters
- Private helper methods with trivial logic