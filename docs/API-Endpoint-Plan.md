# RaceDay API Endpoint Plan

## Revision History

| Date | Change | Reason |
|---|---|---|
| 18 October 2026 | Documented session-based authentication and the logout endpoint, added the organiser dashboard of owned events, aligned routes, request fields and status codes with the implemented API, and added result-history context fields. | Lecturer feedback on the Part 1 plan. |
| 18 October 2026 | Added the enrolment status endpoint (`PUT /api/events/{eventId}/enrolments/{enrolmentId}/status`), enrolment counts on events and the organiser dashboard, and corrected the event delete response to 204. | Endpoints needed by the Part 3 organiser enrolments page, dashboard and colour-coded statuses. |




## Overview

The RaceDay API provides RESTful endpoints for authentication, user profiles, file uploads, events, event categories, participant enrolments, and race results.

The API uses role-based access control with two user roles:

- **Organiser** - manages events, categories, enrolments and results.
- **Participant** - manages their profile, profile picture, browses events, enrols in events and views their own results.

All protected endpoints require an authenticated session. Data is stored with Entity Framework Core (Microsoft, n.d.-b), images are stored in Azure Blob Storage (Microsoft, n.d.-a), and the endpoints are documented through Swagger (Swashbuckle.AspNetCore contributors, n.d.). This plan is written in Markdown (GitHub, n.d.-b).


## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/auth/register` | Creates a new user account with the selected Organiser or Participant role. | None (public) | `{ "firstName": "string", "lastName": "string", "email": "string", "password": "string", "phoneNumber": "string", "roleName": "Participant" }` | **201 Created** - Account created successfully. **400 Bad Request** - Invalid or incomplete data, including an invalid role. **409 Conflict** - Email already registered. |
| POST | `/api/auth/login` | Authenticates a registered user and establishes a server-side session using the session cookie. | None (public) | `{ "email": "string", "password": "string" }` | **200 OK** - Login successful; the session cookie is established. **400 Bad Request** - Invalid request. **401 Unauthorized** - Invalid email or password. |
| POST | `/api/auth/logout` | Clears the current session if one exists. | Any | None | **200 OK** - Session cleared. |



## User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/users/me` | Retrieves the authenticated user's profile information, including their profile picture URL when available. | Any (logged in) | None | **200 OK** - Profile returned successfully. **401 Unauthorized** - Authentication required. **404 Not Found** - User profile not found. |
| PUT | `/api/users/me` | Updates the authenticated user's profile information. | Any (logged in) | `{ "firstName": "string", "lastName": "string", "phoneNumber": "string" }` | **200 OK** - Profile updated successfully. **400 Bad Request** - Invalid or incomplete data. **401 Unauthorized** - Authentication required. |

## File Uploads

The API handles image uploads through Azure Blob Storage. After a successful upload, it saves the resulting Blob Storage URL in the corresponding database field (Microsoft, n.d.-a).

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/users/me/profile-picture` | Uploads or replaces the authenticated user's profile picture. The API stores the image in Azure Blob Storage and saves the resulting URL in `Users.ProfilePictureUrl`. | Any (logged in) | `multipart/form-data` containing the profile image file. | **200 OK** - Profile picture uploaded successfully and URL returned. **400 Bad Request** - Invalid file or request. **401 Unauthorized** - Authentication required. **503 Service Unavailable** - Storage is not configured or the external storage service is unavailable. |
| POST | `/api/events/{eventId}/banner` | Uploads or replaces the banner image for an event owned by the authenticated organiser. The API stores the image in Azure Blob Storage and saves the resulting URL in `Events.BannerImageUrl`. | Organiser | `multipart/form-data` containing the banner image file. | **200 OK** - Event banner uploaded successfully and URL returned. **400 Bad Request** - Invalid file or request. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Event does not exist. **503 Service Unavailable** - Storage is not configured or the external storage service is unavailable. |



## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/events` | Retrieves a list of upcoming and available events for participants to browse, with optional filtering by event type, date and location. Each event includes its total `enrolmentCount`. | None (public) | Query parameters such as `eventType`, `eventDate`, and `location`. | **200 OK** - List of events returned. **500 Internal Server Error** - Unable to retrieve events. |
| GET | `/api/events/{id}` | Retrieves detailed information about a specific event, including its total `enrolmentCount`. | None (public) | None | **200 OK** - Event details returned. **404 Not Found** - Event does not exist. |
| POST | `/api/events` | Creates a new event. A banner image is uploaded separately through the event-banner endpoint. | Organiser | `{ "eventName": "string", "description": "string", "eventDate": "YYYY-MM-DD", "location": "string", "distanceKm": 0.00, "eventType": "Run" }` | **201 Created** - Event created successfully. **400 Bad Request** - Invalid or incomplete data. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required. **409 Conflict** - An event with the same name already exists. |
| PUT | `/api/events/{id}` | Updates an existing event owned by the authenticated organiser. A banner image is uploaded separately through the event-banner endpoint. | Organiser | `{ "eventName": "string", "description": "string", "eventDate": "YYYY-MM-DD", "location": "string", "distanceKm": 0.00, "eventType": "Run" }` | **200 OK** - Event updated successfully. **400 Bad Request** - Invalid data. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Event does not exist. **409 Conflict** - An event with the same name already exists. |
| DELETE | `/api/events/{id}` | Deletes an existing event owned by the authenticated organiser. | Organiser | None | **204 No Content** - Event deleted successfully. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Event does not exist. **409 Conflict** - The event cannot be deleted while dependent categories or enrolments remain. |


## Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/events/{eventId}/categories` | Retrieves all categories available for a specific event. | None (public) | None | **200 OK** - Categories returned successfully. **404 Not Found** - Event does not exist. |
| GET | `/api/categories/{categoryId}` | Retrieves details of a specific event category. | None (public) | None | **200 OK** - Category details returned. **404 Not Found** - Category does not exist. |
| POST | `/api/events/{eventId}/categories` | Creates a new category for an event. | Organiser | `{ "categoryName": "string", "categoryType": "string", "minAge": 0, "maxAge": 0, "categoryDistanceKm": 0.00 }` | **201 Created** - Category created successfully. **400 Bad Request** - Invalid or incomplete data. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Event does not exist. **409 Conflict** - A category with the same name already exists for the event. |
| PUT | `/api/categories/{categoryId}` | Updates an existing event category. | Organiser | `{ "categoryName": "string", "categoryType": "string", "minAge": 0, "maxAge": 0, "categoryDistanceKm": 0.00 }` | **200 OK** - Category updated successfully. **400 Bad Request** - Invalid data. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or category belongs to an event not owned by organiser. **404 Not Found** - Category does not exist. **409 Conflict** - A category with the same name already exists for the event. |
| DELETE | `/api/categories/{categoryId}` | Deletes an existing event category. | Organiser | None | **204 No Content** - Category deleted successfully. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or category belongs to an event not owned by organiser. **404 Not Found** - Category does not exist. **409 Conflict** - The category is referenced by one or more enrolments. |


## Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/enrolments` | Retrieves all events the authenticated participant has enrolled in, including the selected category, enrolment status and enrolment date. | Participant | None | **200 OK** - Participant enrolments returned, including event, category and enrolment status. **401 Unauthorized** - Authentication required. **403 Forbidden** - Participant role required. |
| GET | `/api/enrolments/mine` | Alias of `GET /api/enrolments`; returns the authenticated participant's own enrolments. | Participant | None | **200 OK** - Participant enrolments returned. **401 Unauthorized** - Authentication required. **403 Forbidden** - Participant role required. |
| POST | `/api/enrolments` | Alternative to `POST /api/events/{eventId}/enrolments`; enrols the authenticated participant using event and category IDs in the body. | Participant | `{ "eventId": 0, "categoryId": 0 }` | **201 Created** - Enrolment created. **400 Bad Request** - Invalid request or category/event mismatch. **401 Unauthorized** - Authentication required. **403 Forbidden** - Participant role required. **409 Conflict** - Duplicate enrolment. |
| GET | `/api/events/{eventId}/enrolments` | Retrieves all participant enrolments for a specific event. | Organiser | None | **200 OK** - Event enrolments returned. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Event does not exist. |
| GET | `/api/organiser/events/{eventId}/enrolments` | Alias of `GET /api/events/{eventId}/enrolments`; retrieves enrolments for an event owned by the authenticated organiser. | Organiser | None | **200 OK** - Event enrolments returned. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Event does not exist. |
| GET | `/api/enrolments/{enrolmentId}` | Retrieves details of a specific enrolment, including its current status. | Participant or Organiser | None | **200 OK** - Enrolment details returned. **401 Unauthorized** - Authentication required. **403 Forbidden** - User does not have permission to view the enrolment. **404 Not Found** - Enrolment does not exist. |
| POST | `/api/events/{eventId}/enrolments` | Enrols the authenticated participant in an event using a selected category. The enrolment is created with its current system-controlled status. | Participant | `{ "categoryId": 0 }` | **201 Created** - Enrolment created successfully with its current status. **400 Bad Request** - Invalid category or request data. **401 Unauthorized** - Authentication required. **403 Forbidden** - Participant role required. **404 Not Found** - Event or category does not exist. **409 Conflict** - Participant is already enrolled in the event. |
| PUT | `/api/events/{eventId}/enrolments/{enrolmentId}/status` | Changes the status of an enrolment for an event owned by the authenticated organiser. New enrolments start as `Pending`. | Organiser | `{ "enrolmentStatus": "Pending" }` or `{ "enrolmentStatus": "Confirmed" }` | **200 OK** - Status updated; the enrolment ID, event ID, participant ID and new status are returned. **400 Bad Request** - Status is not `Pending` or `Confirmed`. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Event does not exist, or the enrolment does not belong to the event. |
| DELETE | `/api/enrolments/{enrolmentId}` | Removes an enrolment from an event. | Participant | None | **204 No Content** - Enrolment removed successfully. **401 Unauthorized** - Authentication required. **403 Forbidden** - User does not have permission to remove the enrolment. **404 Not Found** - Enrolment does not exist. **409 Conflict** - A result exists for the enrolment and prevents deletion. |



## Dashboards

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/participant/dashboard` | Confirms the participant session and returns the participant's user ID and role. Enrolment details are read from `GET /api/enrolments`. | Participant | None | **200 OK** - Participant dashboard returned. **401 Unauthorized** - Authentication required. **403 Forbidden** - Participant role required. |
| GET | `/api/organiser/dashboard` | Retrieves the authenticated organiser's own events, ordered by date, with the total `enrolmentCount` for each event. | Organiser | None | **200 OK** - Organiser dashboard returned with the organiser's events and their enrolment counts. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required. |

## Results

Results are visible only to the organiser who owns the event and to the participant who earned them. The brief limits participants to their own results, so there is no public leaderboard endpoint.

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/results/me` | Retrieves the authenticated participant's race results and performance history, including event date, category name, and the total number of finishers in each relevant event/category for placing context (for example, 47th of 312). | Participant | None | **200 OK** - Participant results returned successfully, including `eventDate`, `categoryName`, and `totalFinishers` for each result. **401 Unauthorized** - Authentication required. **403 Forbidden** - Participant role required. |
| GET | `/api/events/{eventId}/results` | Retrieves race results for participants enrolled in a specific event. | Organiser | None | **200 OK** - Event results returned successfully. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Event does not exist. |
| GET | `/api/enrolments/{enrolmentId}/result` | Retrieves the result associated with a specific enrolment. | Participant or Organiser | None | **200 OK** - Result returned successfully. **401 Unauthorized** - Authentication required. **403 Forbidden** - User does not have permission to view the result. **404 Not Found** - Enrolment or result does not exist. |
| POST | `/api/enrolments/{enrolmentId}/result` | Records a race result for a participant's enrolment. | Organiser | `{ "finishPosition": 0, "finishTime": "HH:MM:SS" }` | **201 Created** - Result recorded successfully. **400 Bad Request** - Invalid result data. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Enrolment does not exist. **409 Conflict** - A result already exists for this enrolment. |
| PUT | `/api/enrolments/{enrolmentId}/result` | Updates an existing race result. | Organiser | `{ "finishPosition": 0, "finishTime": "HH:MM:SS" }` | **200 OK** - Result updated successfully. **400 Bad Request** - Invalid result data. **401 Unauthorized** - Authentication required. **403 Forbidden** - Organiser role required or event not owned by organiser. **404 Not Found** - Result does not exist. **409 Conflict** - The updated result conflicts with an existing result or database constraint. |


## API Conventions and Access Control

### Authentication

Protected endpoints require an authenticated user.

The API uses session-based authentication, not JWT bearer tokens. A successful login establishes a server-side session identified by the `.RaceDay.Session` cookie. The authenticated user's identity and role are used to determine access to protected resources. The cookie is HTTP-only and configured with `SameSite=Lax`; the session idle timeout is 30 minutes (Microsoft, n.d.-c).

### Role-Based Access

The API has two roles:

- **Organiser**
  - Create, update and delete events.
  - Create, update and delete event categories.
  - View enrolments for events they manage.
  - Record and update race results.

- **Participant**
  - View available events and categories.
  - View and update their own profile.
  - Enrol in events.
  - View their own enrolments and race results.

Users must not be able to perform operations that belong to the other role.

### Common HTTP Status Codes

The status-code meanings follow the HTTP semantics in RFC 9110 (IETF, 2022).

| Status Code | Meaning |
|---|---|
| 200 OK | Request completed successfully. |
| 201 Created | A new resource was successfully created. |
| 204 No Content | Resource was successfully deleted and no response body is required. |
| 400 Bad Request | Request contains invalid or incomplete data. |
| 401 Unauthorized | Authentication is required or the supplied login credentials are invalid. |
| 403 Forbidden | The authenticated user does not have permission to perform the operation. |
| 404 Not Found | The requested resource does not exist. |
| 409 Conflict | The request conflicts with an existing resource or state, such as duplicate registration. |
| 500 Internal Server Error | An unexpected server-side error occurred. |
| 503 Service Unavailable | A required external service, such as Azure Blob Storage, is not configured or unavailable. |

### Validation and Data Integrity

The API validates incoming request data before modifying the database.

Examples include:

- Email addresses must be unique.
- Event type must be one of `Run`, `Walk`, or `Cycle`.
- Categories must belong to the selected event.
- A participant cannot enrol in the same event more than once.
- A result can only be recorded for an existing enrolment.
- An enrolment can have at most one result.
- Organisers can only manage events and related resources that they own.
- Enrolment status must be `Pending` or `Confirmed`.

### Response Format

Successful responses return JSON representations of the requested or created resources where a response body is required.

Error responses return an appropriate HTTP status code and a JSON error message describing the reason for the failure.

### API Documentation

All endpoints are documented and testable through Swagger/OpenAPI (Swashbuckle.AspNetCore contributors, n.d.). The behaviour described in this plan is covered by automated xUnit tests (xUnit.net contributors, n.d.) that run in a GitHub Actions workflow (GitHub, n.d.-a).

## References

GitHub (n.d.-a) *About GitHub Actions*. Available at: https://docs.github.com/en/actions/about-github-actions (Accessed: 18 October 2026).

GitHub (n.d.-b) *Basic writing and formatting syntax*. Available at: https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax (Accessed: 18 October 2026).

IETF (2022) *RFC 9110: HTTP Semantics*. Available at: https://www.rfc-editor.org/rfc/rfc9110 (Accessed: 18 October 2026).

Microsoft (n.d.-a) *Azure Blob Storage documentation*. Available at: https://learn.microsoft.com/en-us/azure/storage/blobs/ (Accessed: 18 October 2026).

Microsoft (n.d.-b) *Entity Framework Core documentation*. Available at: https://learn.microsoft.com/en-us/ef/core/ (Accessed: 18 October 2026).

Microsoft (n.d.-c) *Session and state management in ASP.NET Core*. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state (Accessed: 18 October 2026).

Swashbuckle.AspNetCore contributors (n.d.) *Swashbuckle.AspNetCore*. Available at: https://github.com/domaindrivendev/Swashbuckle.AspNetCore (Accessed: 18 October 2026).

xUnit.net contributors (n.d.) *xUnit.net*. Available at: https://xunit.net/ (Accessed: 18 October 2026).
