# Authentication

There are a number of ways to secure the Experiment Catalog API. This guide will describe some of the common methods.

Regardless of the method used for authentication, this is the order in which tokens are evaluated:

- Authorization header with a Bearer token
- Cookie specified by `OIDC_VALIDATE_COOKIE`
- Header specified by `OIDC_VALIDATE_HEADER`

## Anonymous

By default, the Experiment Catalog does not require authentication. This is suitable for development and testing scenarios, or when the catalog is hosted in a secure environment where access is controlled by other means.

## EasyAuth

If hosting the catalog in Azure App Service or Container Apps, EasyAuth can be configured using the Microsoft identity provider or the OpenID Connect provider. This is sufficient for providing a secure solution.

When using the Microsoft identity provider, the X-MS-TOKEN-AAD-ID-TOKEN and/or X-MS-TOKEN-AAD-ACCESS-TOKEN headers are passed to the underlying application. The Experiment Catalog can be configured to validate the token signature by setting `OIDC_AUTHORITY`. You can optionally validate other claims such as `OIDC_AUDIENCES`, `OIDC_ISSUERS`, and `OIDC_VALIDATE_LIFETIME`. While validation of the token is not strictly necessary when using EasyAuth, it does provide an additional layer of security, for example, if the EasyAuth configuration is disabled or changed.

## OpenID Connect (OIDC)

If you want to use OIDC authentication directly, you can configure the Experiment Catalog to use an OIDC provider using a PKCE code flow such as Azure AD, Auth0, or Okta. You will need to set the following configuration values:

- `OIDC_AUTHORITY`
- `OIDC_CLIENT_ID`
- `OIDC_CLIENT_SECRET` (this may be optional for some providers)

With those parameters set, the Experiment Catalog API provides the following endpoints:

- `GET /auth/login`: Redirects the user to the OIDC provider's authorization endpoint to initiate the login process.

- `GET /auth/callback`: Handles the callback from the OIDC provider after the user has authenticated. This endpoint exchanges the authorization code for tokens and sets a secure cookie with the `id_token`. It then redirects the user to the home page or a specified return URL.

- `POST /auth/logout`: Logs the user out by clearing the authentication cookie and redirecting to the OIDC provider's logout endpoint.

- `GET /auth/status`: Returns information about whether authentication is required and whether the user is currently authenticated.

In addition to the above configuration values, you may also want to set:

- `OIDC_AUDIENCES`
- `OIDC_ISSUERS`
- `OIDC_VALIDATE_LIFETIME`
