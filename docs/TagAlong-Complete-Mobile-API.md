# TagAlong Complete Mobile API Documentation

## Table of Contents
1. [Overview](#overview)
2. [Authentication](#authentication)
3. [User Flows](#user-flows)
4. [API Reference by Service](#api-reference)
5. [SignalR Real-time Hubs](#signalr-hubs)
6. [Error Handling](#error-handling)
7. [Mobile UI Screens](#mobile-ui-screens)

---

## Overview

TagAlong is a peer-to-peer delivery and errand platform with two main use cases:

1. **Scheduled Trips** - Users traveling between locations can carry packages for others
2. **On-Demand Availability** - Users can mark themselves available for errands nearby

### Service Architecture

| Service | Port | Purpose |
|---------|------|---------|
| Identity | 5001 | Authentication, registration, login |
| User | 5002 | User profiles, availability, location |
| Trip | 5003 | Scheduled trips, routes |
| Package | 5004 | Package requests, deliveries |
| Payment | 5005 | Payments, earnings, transactions |
| Review | 5006 | Ratings and reviews |
| Messaging | 5007 | In-app chat, price negotiation |
| Notification | 5008 | Push notifications, in-app notifications |
| Report | 5009 | User/delivery reports |
| Configuration | 5010 | App settings, fee configurations |

---

## Authentication

All authenticated endpoints require JWT Bearer token:

```
Authorization: Bearer <jwt_token>
```

### Token Structure
```json
{
  "sub": "user-guid",
  "email": "user@example.com",
  "name": "John Doe",
  "exp": 1707235200,
  "iss": "TagAlong",
  "aud": "TagAlong.Mobile"
}
```

### Token Lifecycle
- **Access Token**: Valid for 1 hour
- **Refresh Token**: Valid for 7 days
- Store refresh token securely (Keychain/Keystore)

---

## User Flows

### Flow 1: Onboarding (New User)

```
┌─────────────────────────────────────────────────────────────┐
│                    ONBOARDING FLOW                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. WELCOME SCREEN                                          │
│     └── [Get Started] / [Login]                             │
│                                                             │
│  2. REGISTRATION                                            │
│     ├── Email & Password                                    │
│     ├── Full Name                                           │
│     ├── Phone Number                                        │
│     └── POST /api/auth/register                             │
│                                                             │
│  3. EMAIL VERIFICATION (optional)                           │
│     └── Verify email link                                   │
│                                                             │
│  4. PROFILE SETUP                                           │
│     ├── Profile Photo                                       │
│     ├── Bio/Description                                     │
│     └── PUT /api/users/me                                   │
│                                                             │
│  5. LOCATION PERMISSION                                     │
│     └── Request GPS permission                              │
│                                                             │
│  6. NOTIFICATION PERMISSION                                 │
│     └── Request push notification permission                │
│                                                             │
│  7. HOME SCREEN                                             │
│     └── Ready to use app                                    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

### Flow 2: Sender Requesting Delivery

```
┌─────────────────────────────────────────────────────────────┐
│              SENDER: REQUEST DELIVERY FLOW                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. HOME SCREEN                                             │
│     └── Tap [Send Package]                                  │
│                                                             │
│  2. PACKAGE DETAILS                                         │
│     ├── Package description                                 │
│     ├── Package size (Small/Medium/Large)                   │
│     ├── Package weight                                      │
│     ├── Package photo (optional)                            │
│     └── Is fragile? toggle                                  │
│                                                             │
│  3. PICKUP LOCATION                                         │
│     ├── Use current location OR                             │
│     ├── Search address                                      │
│     ├── Select on map                                       │
│     └── Add pickup instructions                             │
│                                                             │
│  4. DELIVERY LOCATION                                       │
│     ├── Search address                                      │
│     ├── Select on map                                       │
│     ├── Recipient name                                      │
│     ├── Recipient phone                                     │
│     └── Add delivery instructions                           │
│                                                             │
│  5. FIND TRAVELERS                                          │
│     ├── Option A: Search scheduled trips                    │
│     │   └── GET /api/trips/search                           │
│     ├── Option B: Find available riders nearby              │
│     │   └── GET /api/users/availability/nearby              │
│     └── Show list of matching travelers                     │
│                                                             │
│  6. SELECT TRAVELER                                         │
│     ├── View traveler profile                               │
│     ├── See ratings & reviews                               │
│     ├── See completed deliveries count                      │
│     └── Tap [Request This Traveler]                         │
│                                                             │
│  7. START CONVERSATION                                      │
│     ├── POST /api/conversations                             │
│     ├── Send package details                                │
│     └── Discuss pickup time                                 │
│                                                             │
│  8. PRICE NEGOTIATION                                       │
│     ├── Traveler sends price proposal                       │
│     │   └── POST /api/conversations/{id}/price-proposal     │
│     ├── Counter-offer or accept                             │
│     │   └── POST /api/conversations/{id}/accept-price       │
│     └── Price agreed!                                       │
│                                                             │
│  9. CREATE PACKAGE REQUEST                                  │
│     ├── POST /api/packages                                  │
│     └── Package request created                             │
│                                                             │
│  10. MAKE PAYMENT                                           │
│     ├── Select payment method                               │
│     ├── POST /api/payments                                  │
│     ├── Complete payment                                    │
│     └── POST /api/payments/{id}/confirm                     │
│                                                             │
│  11. TRACK DELIVERY                                         │
│     ├── Real-time location updates                          │
│     ├── Status updates                                      │
│     └── SignalR: /notificationHub                           │
│                                                             │
│  12. DELIVERY COMPLETE                                      │
│     ├── Receive confirmation photo                          │
│     ├── Confirm delivery                                    │
│     └── Leave review                                        │
│         └── POST /api/reviews                               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

### Flow 3: Traveler Accepting Delivery

```
┌─────────────────────────────────────────────────────────────┐
│             TRAVELER: ACCEPT DELIVERY FLOW                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Option A: SCHEDULED TRIP                                   │
│  ─────────────────────────                                  │
│  1. CREATE TRIP                                             │
│     ├── POST /api/trips                                     │
│     ├── Set origin & destination                            │
│     ├── Set departure date/time                             │
│     ├── Set available capacity                              │
│     └── Trip published!                                     │
│                                                             │
│  2. RECEIVE REQUEST                                         │
│     ├── Push notification                                   │
│     ├── View package details                                │
│     └── Start conversation                                  │
│                                                             │
│  Option B: GO AVAILABLE                                     │
│  ──────────────────────                                     │
│  1. TOGGLE AVAILABILITY                                     │
│     ├── POST /api/users/availability/me                     │
│     ├── Share current location                              │
│     ├── Set duration (1-8 hours)                            │
│     └── Now discoverable!                                   │
│                                                             │
│  2. RECEIVE REQUEST                                         │
│     ├── Push notification                                   │
│     ├── SignalR real-time alert                             │
│     └── View sender's package request                       │
│                                                             │
│  COMMON FLOW (After Receiving Request):                     │
│  ─────────────────────────────────────                      │
│  3. VIEW REQUEST DETAILS                                    │
│     ├── Package info                                        │
│     ├── Pickup & delivery locations                         │
│     ├── Sender profile & ratings                            │
│     └── Distance calculation                                │
│                                                             │
│  4. NEGOTIATE PRICE                                         │
│     ├── Send price proposal                                 │
│     │   └── POST /api/conversations/{id}/price-proposal     │
│     ├── Wait for acceptance                                 │
│     └── Price confirmed!                                    │
│                                                             │
│  5. ACCEPT DELIVERY                                         │
│     ├── POST /api/deliveries                                │
│     └── Delivery assigned to you                            │
│                                                             │
│  6. PICKUP PACKAGE                                          │
│     ├── Navigate to pickup location                         │
│     ├── Meet sender                                         │
│     ├── Collect package                                     │
│     ├── Take photo proof                                    │
│     └── PUT /api/deliveries/{id}/status                     │
│         └── status: "PickedUp"                              │
│                                                             │
│  7. IN TRANSIT                                              │
│     ├── Update location periodically                        │
│     │   └── SignalR: UpdateLocation()                       │
│     └── Status: "InTransit"                                 │
│                                                             │
│  8. DELIVER PACKAGE                                         │
│     ├── Navigate to delivery location                       │
│     ├── Meet recipient                                      │
│     ├── Hand over package                                   │
│     ├── Take delivery photo                                 │
│     └── PUT /api/deliveries/{id}/status                     │
│         └── status: "Delivered"                             │
│                                                             │
│  9. RECEIVE PAYMENT                                         │
│     ├── Payment released to wallet                          │
│     ├── GET /api/payments/earnings                          │
│     └── View earnings                                       │
│                                                             │
│  10. GET REVIEWED                                           │
│     ├── Sender leaves review                                │
│     ├── Leave review for sender                             │
│     └── Ratings updated                                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

### Flow 4: Price Negotiation

```
┌─────────────────────────────────────────────────────────────┐
│                 PRICE NEGOTIATION FLOW                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  SENDER                          TRAVELER                   │
│  ──────                          ────────                   │
│                                                             │
│  1. Start conversation ─────────────────────────────────>   │
│     POST /api/conversations                                 │
│     Body: { participantId, packageDetails }                 │
│                                                             │
│  2. Describe package  ──────────────────────────────────>   │
│     POST /api/conversations/{id}/messages                   │
│     "I need to send a laptop to Ikeja"                      │
│                                                             │
│                     <──────────────────────────────  3.     │
│                           View package details              │
│                           Calculate distance                │
│                                                             │
│                     <──────────────────────────────  4.     │
│                           Send price proposal               │
│                           POST /api/conversations           │
│                                  /{id}/price-proposal       │
│                           { amount: 2500, currency: "NGN" } │
│                                                             │
│  5. View proposal                                           │
│     ├── Accept: POST /accept-price ─────────────────────>   │
│     ├── Counter: Send message with new price ───────────>   │
│     └── Decline: Send message ──────────────────────────>   │
│                                                             │
│  If Counter-offer:                                          │
│  ────────────────                                           │
│  6. "Can you do 2000?" ─────────────────────────────────>   │
│                                                             │
│                     <──────────────────────────────  7.     │
│                           "Final offer: 2200"               │
│                           POST /price-proposal              │
│                           { amount: 2200 }                  │
│                                                             │
│  8. Accept price ───────────────────────────────────────>   │
│     POST /api/conversations/{id}/accept-price               │
│                                                             │
│  ═══════════════════════════════════════════════════════    │
│                    PRICE AGREED: ₦2,200                     │
│  ═══════════════════════════════════════════════════════    │
│                                                             │
│  9. Create package request                                  │
│     POST /api/packages                                      │
│     { agreedPrice: 2200, ... }                              │
│                                                             │
│  10. Initiate payment                                       │
│      POST /api/payments                                     │
│      { amount: 2200, deliveryId, method: "card" }           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## API Reference

### Identity Service (Port 5001)

#### Register New User
```
POST /api/auth/register
```

**Request:**
```json
{
  "email": "john.doe@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+2348012345678"
}
```

**Response (201 Created):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g...",
  "expiresAt": "2026-02-06T12:00:00Z"
}
```

**Validation Rules:**
- Email: Valid email format, unique
- Password: Min 8 chars, 1 uppercase, 1 lowercase, 1 number, 1 special char
- Phone: Valid Nigerian format (+234...)

---

#### Login
```
POST /api/auth/login
```

**Request:**
```json
{
  "email": "john.doe@example.com",
  "password": "SecurePass123!"
}
```

**Response (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g...",
  "expiresAt": "2026-02-06T12:00:00Z"
}
```

---

#### Google Sign-In
```
POST /api/auth/google
```

**Request:**
```json
{
  "idToken": "google-oauth-id-token-here"
}
```

**Response:** Same as login

---

#### Refresh Token
```
POST /api/auth/refresh
```

**Request:**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g..."
}
```

**Response:**
```json
{
  "accessToken": "new-access-token...",
  "refreshToken": "new-refresh-token...",
  "expiresAt": "2026-02-06T13:00:00Z"
}
```

---

#### Logout
```
POST /api/auth/logout
Authorization: Bearer <token>
```

**Response:** 204 No Content

---

### User Service (Port 5002)

#### Get My Profile
```
GET /api/users/me
Authorization: Bearer <token>
```

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "authUserId": "auth-user-guid",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+2348012345678",
  "profileImageUrl": "https://storage.tagalong.com/profiles/john.jpg",
  "bio": "Reliable traveler based in Lagos",
  "isVerified": true,
  "averageRating": 4.8,
  "totalRatings": 25,
  "completedDeliveries": 42,
  "completedTrips": 15,
  "createdAt": "2025-01-15T10:00:00Z"
}
```

---

#### Update My Profile
```
PUT /api/users/me
Authorization: Bearer <token>
```

**Request:**
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+2348012345678",
  "bio": "Reliable traveler based in Lagos. Always on time!",
  "profileImageUrl": "https://storage.tagalong.com/profiles/john-new.jpg"
}
```

---

#### Get User Public Profile
```
GET /api/users/{userId}
```

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "firstName": "John",
  "lastName": "D.",
  "profileImageUrl": "https://storage.tagalong.com/profiles/john.jpg",
  "bio": "Reliable traveler based in Lagos",
  "isVerified": true,
  "averageRating": 4.8,
  "totalRatings": 25,
  "completedDeliveries": 42,
  "completedTrips": 15,
  "memberSince": "2025-01-15"
}
```

---

#### Get Availability Status
```
GET /api/users/availability/me
Authorization: Bearer <token>
```

**Response:**
```json
{
  "isAvailable": true,
  "latitude": 6.5244,
  "longitude": 3.3792,
  "locationName": "Victoria Island, Lagos",
  "availabilityStartedAt": "2026-02-06T10:00:00Z",
  "availabilityExpiresAt": "2026-02-06T14:00:00Z",
  "locationUpdatedAt": "2026-02-06T10:05:00Z",
  "maxTravelRadiusKm": 10.0,
  "allowLocationSharing": true
}
```

---

#### Set Availability
```
POST /api/users/availability/me
Authorization: Bearer <token>
```

**Request (Go Online):**
```json
{
  "isAvailable": true,
  "latitude": 6.5244,
  "longitude": 3.3792,
  "locationName": "Victoria Island, Lagos",
  "durationMinutes": 240
}
```

**Request (Go Offline):**
```json
{
  "isAvailable": false
}
```

---

#### Update Location
```
PUT /api/users/availability/me/location
Authorization: Bearer <token>
```

**Request:**
```json
{
  "latitude": 6.5250,
  "longitude": 3.3800,
  "locationName": "Lekki Phase 1, Lagos"
}
```

---

#### Search Nearby Available Users
```
GET /api/users/availability/nearby?latitude=6.5244&longitude=3.3792&radiusKm=5&page=1&pageSize=20
```

**Response:**
```json
{
  "users": [
    {
      "id": "user-guid-1",
      "firstName": "Jane",
      "lastName": "Smith",
      "profileImageUrl": "https://...",
      "averageRating": 4.9,
      "totalRatings": 32,
      "completedDeliveries": 56,
      "completedTrips": 20,
      "isVerified": true,
      "distanceKm": 1.2,
      "locationName": "Lekki, Lagos",
      "locationUpdatedAt": "2026-02-06T10:25:00Z"
    }
  ],
  "totalCount": 8,
  "page": 1,
  "pageSize": 20
}
```

---

### Trip Service (Port 5003)

#### Create Trip
```
POST /api/trips
Authorization: Bearer <token>
```

**Request:**
```json
{
  "originName": "Victoria Island, Lagos",
  "originLatitude": 6.4281,
  "originLongitude": 3.4219,
  "destinationName": "Ibadan, Oyo",
  "destinationLatitude": 7.3775,
  "destinationLongitude": 3.9470,
  "departureTime": "2026-02-10T08:00:00Z",
  "estimatedArrivalTime": "2026-02-10T10:30:00Z",
  "availableCapacity": "Medium",
  "vehicleType": "Car",
  "vehicleDescription": "Toyota Camry, Silver",
  "pricePerKg": 500,
  "notes": "Leaving early morning, can make one stop",
  "stops": [
    {
      "name": "Shagamu, Ogun",
      "latitude": 6.8500,
      "longitude": 3.6500,
      "estimatedArrival": "2026-02-10T09:00:00Z"
    }
  ]
}
```

**Response:**
```json
{
  "id": "trip-guid",
  "travelerId": "user-guid",
  "status": "Scheduled",
  "originName": "Victoria Island, Lagos",
  "destinationName": "Ibadan, Oyo",
  "departureTime": "2026-02-10T08:00:00Z",
  "createdAt": "2026-02-06T10:00:00Z"
}
```

---

#### Search Trips
```
GET /api/trips/search?originLat=6.4281&originLon=3.4219&destLat=7.3775&destLon=3.9470&date=2026-02-10&radiusKm=10
```

**Response:**
```json
{
  "trips": [
    {
      "id": "trip-guid",
      "traveler": {
        "id": "user-guid",
        "firstName": "John",
        "lastName": "D.",
        "profileImageUrl": "https://...",
        "averageRating": 4.8,
        "completedDeliveries": 42,
        "isVerified": true
      },
      "originName": "Victoria Island, Lagos",
      "destinationName": "Ibadan, Oyo",
      "departureTime": "2026-02-10T08:00:00Z",
      "estimatedArrivalTime": "2026-02-10T10:30:00Z",
      "availableCapacity": "Medium",
      "vehicleType": "Car",
      "pricePerKg": 500,
      "distanceFromOriginKm": 2.1,
      "distanceFromDestKm": 1.5
    }
  ],
  "totalCount": 3,
  "page": 1,
  "pageSize": 20
}
```

---

#### Get Trip Details
```
GET /api/trips/{tripId}
```

**Response:**
```json
{
  "id": "trip-guid",
  "traveler": {
    "id": "user-guid",
    "firstName": "John",
    "lastName": "Doe",
    "profileImageUrl": "https://...",
    "averageRating": 4.8,
    "isVerified": true
  },
  "status": "Scheduled",
  "originName": "Victoria Island, Lagos",
  "originLatitude": 6.4281,
  "originLongitude": 3.4219,
  "destinationName": "Ibadan, Oyo",
  "destinationLatitude": 7.3775,
  "destinationLongitude": 3.9470,
  "departureTime": "2026-02-10T08:00:00Z",
  "estimatedArrivalTime": "2026-02-10T10:30:00Z",
  "availableCapacity": "Medium",
  "vehicleType": "Car",
  "vehicleDescription": "Toyota Camry, Silver",
  "pricePerKg": 500,
  "notes": "Leaving early morning",
  "stops": [...],
  "createdAt": "2026-02-06T10:00:00Z"
}
```

---

#### Get My Trips
```
GET /api/trips/my-trips?status=Scheduled&page=1&pageSize=20
Authorization: Bearer <token>
```

---

#### Update Trip Status
```
PUT /api/trips/{tripId}/status
Authorization: Bearer <token>
```

**Request:**
```json
{
  "status": "InProgress"
}
```

**Status Values:** `Scheduled`, `InProgress`, `Completed`, `Cancelled`

---

### Package Service (Port 5004)

#### Create Package Request
```
POST /api/packages
Authorization: Bearer <token>
```

**Request:**
```json
{
  "description": "Laptop computer in protective case",
  "category": "Electronics",
  "size": "Medium",
  "weight": 3.5,
  "isFragile": true,
  "imageUrl": "https://storage.tagalong.com/packages/laptop.jpg",

  "pickupLocationName": "Victoria Island, Lagos",
  "pickupLatitude": 6.4281,
  "pickupLongitude": 3.4219,
  "pickupInstructions": "Call when you arrive, gate code is 1234",

  "deliveryLocationName": "Ibadan, Oyo",
  "deliveryLatitude": 7.3775,
  "deliveryLongitude": 3.9470,
  "deliveryInstructions": "Leave with security if recipient not available",

  "recipientName": "Jane Smith",
  "recipientPhone": "+2348087654321",

  "preferredPickupTime": "2026-02-10T08:00:00Z",
  "preferredDeliveryTime": "2026-02-10T12:00:00Z",

  "agreedPrice": 2500,
  "tripId": "trip-guid-optional"
}
```

**Response:**
```json
{
  "id": "package-guid",
  "status": "Pending",
  "description": "Laptop computer in protective case",
  "pickupLocationName": "Victoria Island, Lagos",
  "deliveryLocationName": "Ibadan, Oyo",
  "agreedPrice": 2500,
  "createdAt": "2026-02-06T10:00:00Z"
}
```

---

#### Search Packages (for Travelers)
```
GET /api/packages/search?latitude=6.4281&longitude=3.4219&radiusKm=10&page=1&pageSize=20
```

**Response:**
```json
{
  "packages": [
    {
      "id": "package-guid",
      "sender": {
        "id": "user-guid",
        "firstName": "Alice",
        "lastName": "W.",
        "averageRating": 4.7,
        "isVerified": true
      },
      "description": "Documents for office",
      "category": "Documents",
      "size": "Small",
      "weight": 0.5,
      "isFragile": false,
      "pickupLocationName": "Lekki, Lagos",
      "deliveryLocationName": "Ikeja, Lagos",
      "distanceFromYouKm": 2.3,
      "estimatedPrice": 1500,
      "preferredPickupTime": "2026-02-06T14:00:00Z"
    }
  ],
  "totalCount": 5,
  "page": 1,
  "pageSize": 20
}
```

---

#### Get My Packages
```
GET /api/packages/my-packages?page=1&pageSize=20
Authorization: Bearer <token>
```

---

### Delivery Service (Port 5004)

#### Create Delivery (Accept Package)
```
POST /api/deliveries
Authorization: Bearer <token>
```

**Request:**
```json
{
  "packageRequestId": "package-guid",
  "estimatedPickupTime": "2026-02-10T08:00:00Z",
  "estimatedDeliveryTime": "2026-02-10T12:00:00Z"
}
```

**Response:**
```json
{
  "id": "delivery-guid",
  "packageRequestId": "package-guid",
  "travelerId": "user-guid",
  "status": "Pending",
  "createdAt": "2026-02-06T10:00:00Z"
}
```

---

#### Update Delivery Status
```
PUT /api/deliveries/{deliveryId}/status
Authorization: Bearer <token>
```

**Request:**
```json
{
  "status": "PickedUp",
  "proofImageUrl": "https://storage.tagalong.com/proofs/pickup-123.jpg",
  "notes": "Package collected from sender"
}
```

**Status Values:**
| Status | Description |
|--------|-------------|
| `Pending` | Delivery created, waiting for pickup |
| `PickedUp` | Traveler has collected package |
| `InTransit` | Package is being transported |
| `Delivered` | Package delivered to recipient |
| `Confirmed` | Sender confirmed delivery |
| `Cancelled` | Delivery cancelled |

---

#### Get Delivery Details
```
GET /api/deliveries/{deliveryId}
Authorization: Bearer <token>
```

**Response:**
```json
{
  "id": "delivery-guid",
  "packageRequest": {
    "id": "package-guid",
    "description": "Laptop computer",
    "pickupLocationName": "Victoria Island, Lagos",
    "deliveryLocationName": "Ibadan, Oyo",
    "recipientName": "Jane Smith",
    "recipientPhone": "+2348087654321"
  },
  "sender": {
    "id": "sender-guid",
    "firstName": "Alice",
    "lastName": "Williams",
    "phone": "+2348012345678"
  },
  "traveler": {
    "id": "traveler-guid",
    "firstName": "John",
    "lastName": "Doe",
    "phone": "+2348098765432"
  },
  "status": "InTransit",
  "pickupProofImageUrl": "https://...",
  "deliveryProofImageUrl": null,
  "pickedUpAt": "2026-02-10T08:15:00Z",
  "deliveredAt": null,
  "currentLatitude": 6.8500,
  "currentLongitude": 3.6500,
  "lastLocationUpdate": "2026-02-10T09:00:00Z"
}
```

---

#### Get My Deliveries
```
GET /api/deliveries/my-deliveries?role=traveler&status=InTransit&page=1&pageSize=20
Authorization: Bearer <token>
```

**Query Params:**
- `role`: `sender` or `traveler`
- `status`: Filter by status

---

### Payment Service (Port 5005)

#### Initiate Payment
```
POST /api/payments
Authorization: Bearer <token>
```

**Request:**
```json
{
  "deliveryId": "delivery-guid",
  "amount": 2500.00,
  "currency": "NGN",
  "paymentMethod": "card",
  "cardDetails": {
    "cardNumber": "5399XXXXXXXX1234",
    "expiryMonth": "12",
    "expiryYear": "2027",
    "cvv": "123"
  }
}
```

**Response:**
```json
{
  "id": "payment-guid",
  "deliveryId": "delivery-guid",
  "amount": 2500.00,
  "currency": "NGN",
  "status": "Pending",
  "paymentReference": "PAY_abc123xyz",
  "authorizationUrl": "https://checkout.paystack.com/abc123",
  "createdAt": "2026-02-06T10:00:00Z"
}
```

**Payment Methods:** `card`, `bank_transfer`, `wallet`

---

#### Confirm Payment
```
POST /api/payments/{paymentId}/confirm
Authorization: Bearer <token>
```

**Request:**
```json
{
  "transactionReference": "TXN_12345"
}
```

**Response:**
```json
{
  "id": "payment-guid",
  "status": "Completed",
  "confirmedAt": "2026-02-06T10:05:00Z"
}
```

---

#### Get Payment Details
```
GET /api/payments/{paymentId}
Authorization: Bearer <token>
```

---

#### Get My Payments
```
GET /api/payments/my-payments?page=1&pageSize=20
Authorization: Bearer <token>
```

---

#### Get Earnings (Traveler)
```
GET /api/payments/earnings
Authorization: Bearer <token>
```

**Response:**
```json
{
  "totalEarnings": 125000.00,
  "pendingEarnings": 5000.00,
  "availableBalance": 120000.00,
  "currency": "NGN",
  "totalDeliveries": 50
}
```

---

#### Get Spending (Sender)
```
GET /api/payments/spending
Authorization: Bearer <token>
```

**Response:**
```json
{
  "totalSpending": 45000.00,
  "pendingPayments": 2500.00,
  "currency": "NGN",
  "totalPackages": 18
}
```

---

#### Request Refund
```
POST /api/payments/{paymentId}/refund
Authorization: Bearer <token>
```

**Request:**
```json
{
  "reason": "Delivery was cancelled by traveler"
}
```

---

### Messaging Service (Port 5007)

#### Get My Conversations
```
GET /api/conversations?page=1&pageSize=20
Authorization: Bearer <token>
```

**Response:**
```json
{
  "conversations": [
    {
      "id": "conversation-guid",
      "participant": {
        "id": "user-guid",
        "firstName": "John",
        "lastName": "Doe",
        "profileImageUrl": "https://..."
      },
      "lastMessage": {
        "content": "Sure, I can pick it up at 3pm",
        "sentAt": "2026-02-06T10:30:00Z",
        "isFromMe": false
      },
      "unreadCount": 2,
      "packageRequest": {
        "id": "package-guid",
        "description": "Laptop computer"
      },
      "priceProposal": {
        "amount": 2500,
        "status": "Pending"
      }
    }
  ],
  "totalCount": 5,
  "page": 1,
  "pageSize": 20
}
```

---

#### Create Conversation
```
POST /api/conversations
Authorization: Bearer <token>
```

**Request:**
```json
{
  "participantId": "other-user-guid",
  "packageRequestId": "package-guid",
  "initialMessage": "Hi! I saw your trip to Ibadan. Can you help deliver a laptop?"
}
```

---

#### Get Conversation Messages
```
GET /api/conversations/{conversationId}/messages?page=1&pageSize=50
Authorization: Bearer <token>
```

**Response:**
```json
{
  "messages": [
    {
      "id": "message-guid",
      "content": "Hi! I saw your trip to Ibadan.",
      "senderId": "user-guid",
      "sentAt": "2026-02-06T10:00:00Z",
      "type": "Text",
      "isFromMe": true
    },
    {
      "id": "message-guid-2",
      "content": "Sure, what do you need delivered?",
      "senderId": "other-user-guid",
      "sentAt": "2026-02-06T10:02:00Z",
      "type": "Text",
      "isFromMe": false
    },
    {
      "id": "message-guid-3",
      "content": null,
      "senderId": "other-user-guid",
      "sentAt": "2026-02-06T10:10:00Z",
      "type": "PriceProposal",
      "isFromMe": false,
      "priceProposal": {
        "amount": 2500,
        "currency": "NGN",
        "status": "Pending"
      }
    }
  ],
  "totalCount": 15,
  "page": 1,
  "pageSize": 50
}
```

---

#### Send Message
```
POST /api/conversations/{conversationId}/messages
Authorization: Bearer <token>
```

**Request:**
```json
{
  "content": "That sounds good. What time works for pickup?"
}
```

---

#### Send Price Proposal
```
POST /api/conversations/{conversationId}/price-proposal
Authorization: Bearer <token>
```

**Request:**
```json
{
  "amount": 2500,
  "currency": "NGN",
  "notes": "Price includes door-to-door delivery"
}
```

**Response:**
```json
{
  "id": "proposal-guid",
  "conversationId": "conversation-guid",
  "amount": 2500,
  "currency": "NGN",
  "status": "Pending",
  "createdAt": "2026-02-06T10:10:00Z"
}
```

---

#### Accept Price Proposal
```
POST /api/conversations/{conversationId}/accept-price
Authorization: Bearer <token>
```

**Request:**
```json
{
  "priceProposalId": "proposal-guid"
}
```

**Response:**
```json
{
  "id": "proposal-guid",
  "status": "Accepted",
  "acceptedAt": "2026-02-06T10:15:00Z"
}
```

---

### Review Service (Port 5006)

#### Create Review
```
POST /api/reviews
Authorization: Bearer <token>
```

**Request:**
```json
{
  "deliveryId": "delivery-guid",
  "revieweeId": "user-being-reviewed-guid",
  "rating": 5,
  "comment": "Excellent service! Package arrived safely and on time.",
  "reviewerRole": "Sender"
}
```

**Validation:**
- `rating`: 1-5
- `reviewerRole`: `Sender` or `Traveler`
- Can only review after delivery is complete

---

#### Get User Reviews
```
GET /api/reviews/user/{userId}?page=1&pageSize=20
```

**Response:**
```json
{
  "reviews": [
    {
      "id": "review-guid",
      "rating": 5,
      "comment": "Excellent service!",
      "reviewer": {
        "id": "reviewer-guid",
        "firstName": "Alice",
        "lastName": "W.",
        "profileImageUrl": "https://..."
      },
      "reviewerRole": "Sender",
      "createdAt": "2026-02-06T12:00:00Z"
    }
  ],
  "totalCount": 25,
  "page": 1,
  "pageSize": 20
}
```

---

#### Get User Review Statistics
```
GET /api/reviews/user/{userId}/stats
```

**Response:**
```json
{
  "averageRating": 4.8,
  "totalReviews": 25,
  "ratingDistribution": {
    "5": 20,
    "4": 3,
    "3": 1,
    "2": 1,
    "1": 0
  },
  "asSender": {
    "averageRating": 4.9,
    "totalReviews": 10
  },
  "asTraveler": {
    "averageRating": 4.7,
    "totalReviews": 15
  }
}
```

---

### Notification Service (Port 5008)

#### Get My Notifications
```
GET /api/notifications?page=1&pageSize=20
Authorization: Bearer <token>
```

**Response:**
```json
{
  "notifications": [
    {
      "id": "notification-guid",
      "type": "DeliveryStatusUpdate",
      "title": "Package Picked Up",
      "body": "John has picked up your package",
      "data": {
        "deliveryId": "delivery-guid",
        "status": "PickedUp"
      },
      "isRead": false,
      "createdAt": "2026-02-06T10:00:00Z"
    },
    {
      "id": "notification-guid-2",
      "type": "NewMessage",
      "title": "New Message from John",
      "body": "Sure, I can pick it up at 3pm",
      "data": {
        "conversationId": "conversation-guid"
      },
      "isRead": true,
      "createdAt": "2026-02-06T09:30:00Z"
    }
  ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 20
}
```

**Notification Types:**
- `NewMessage`
- `PriceProposal`
- `PriceAccepted`
- `DeliveryRequest`
- `DeliveryAccepted`
- `DeliveryStatusUpdate`
- `PaymentReceived`
- `NewReview`
- `UserNearby`

---

#### Get Unread Count
```
GET /api/notifications/unread/count
Authorization: Bearer <token>
```

**Response:**
```json
{
  "count": 5
}
```

---

#### Mark as Read
```
PUT /api/notifications/{notificationId}/read
Authorization: Bearer <token>
```

---

#### Mark All as Read
```
PUT /api/notifications/read-all
Authorization: Bearer <token>
```

---

### Report Service (Port 5009)

#### Report a User
```
POST /api/reports/user
Authorization: Bearer <token>
```

**Request:**
```json
{
  "reportedUserId": "user-guid",
  "reason": "FraudulentBehavior",
  "description": "User took payment but never showed up for pickup"
}
```

**Reason Values:** `FraudulentBehavior`, `InappropriateBehavior`, `SafetyConcern`, `Spam`, `Other`

---

#### Report a Delivery
```
POST /api/reports/delivery
Authorization: Bearer <token>
```

**Request:**
```json
{
  "deliveryId": "delivery-guid",
  "reason": "DamagedPackage",
  "description": "Package arrived with visible damage"
}
```

---

#### Get My Reports
```
GET /api/reports/my-reports?page=1&pageSize=20
Authorization: Bearer <token>
```

---

### Configuration Service (Port 5010)

#### Get Active Fee Configuration
```
GET /api/fee-configurations/active
```

**Response:**
```json
{
  "id": "config-guid",
  "platformFeePercentage": 10.0,
  "minimumFee": 100.0,
  "maximumFee": 5000.0,
  "currency": "NGN",
  "pricePerKmBase": 50.0,
  "pricePerKgBase": 100.0,
  "surgeMultiplier": 1.0,
  "isActive": true
}
```

---

#### Get App Configuration
```
GET /api/configurations/active
```

**Response:**
```json
[
  {
    "key": "MIN_APP_VERSION",
    "value": "1.2.0",
    "type": "String"
  },
  {
    "key": "MAX_PACKAGE_WEIGHT_KG",
    "value": "50",
    "type": "Number"
  },
  {
    "key": "SUPPORT_EMAIL",
    "value": "support@tagalong.com",
    "type": "String"
  }
]
```

---

## SignalR Real-time Hubs

### Notification Hub
**URL:** `wss://your-server:5008/notificationHub?access_token=<jwt>`

#### Events Received:
```dart
// New notification
hubConnection.on('ReceiveNotification', (args) {
  final notification = args[0];
  // Show push notification / update badge
});

// Delivery status changed
hubConnection.on('DeliveryStatusChanged', (args) {
  final data = args[0];
  // { deliveryId, status, updatedAt }
});

// New message
hubConnection.on('NewMessage', (args) {
  final message = args[0];
  // { conversationId, content, senderId, sentAt }
});
```

---

### Messaging Hub
**URL:** `wss://your-server:5007/messagingHub?access_token=<jwt>`

#### Methods to Call:
```dart
// Send message
await hub.invoke('SendMessage', args: [conversationId, content]);

// Mark conversation as read
await hub.invoke('MarkAsRead', args: [conversationId]);

// Typing indicator
await hub.invoke('StartTyping', args: [conversationId]);
await hub.invoke('StopTyping', args: [conversationId]);
```

#### Events Received:
```dart
hubConnection.on('ReceiveMessage', (args) {
  final message = args[0];
});

hubConnection.on('UserTyping', (args) {
  final data = args[0];
  // { conversationId, userId, isTyping }
});

hubConnection.on('PriceProposalReceived', (args) {
  final proposal = args[0];
});
```

---

### Location Hub
**URL:** `wss://your-server:5002/locationHub?access_token=<jwt>`

#### Methods to Call:
```dart
// Update your location
await hub.invoke('UpdateLocation', args: [lat, lon, locationName]);

// Set availability
await hub.invoke('SetAvailable', args: [lat, lon, locationName, durationMins]);
await hub.invoke('SetUnavailable');

// Subscribe to nearby users
await hub.invoke('SubscribeToNearbyUsers', args: [lat, lon, radiusKm]);
```

#### Events Received:
```dart
hubConnection.on('UserBecameAvailable', (args) {
  final user = args[0];
});

hubConnection.on('UserBecameUnavailable', (args) {
  final userId = args[0];
});

hubConnection.on('ReceiveLocationUpdate', (args) {
  final update = args[0];
  // { userId, latitude, longitude, locationName, updatedAt }
});
```

---

## Error Handling

### Standard Error Response
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "errors": {
    "email": ["Email is already registered"],
    "password": ["Password must be at least 8 characters"]
  }
}
```

### HTTP Status Codes
| Code | Meaning | Action |
|------|---------|--------|
| 200 | Success | Process response |
| 201 | Created | Resource created successfully |
| 204 | No Content | Action completed |
| 400 | Bad Request | Show validation errors |
| 401 | Unauthorized | Redirect to login |
| 403 | Forbidden | Show permission denied |
| 404 | Not Found | Show not found screen |
| 409 | Conflict | Resource conflict (e.g., duplicate) |
| 500 | Server Error | Show generic error, retry later |

---

## Mobile UI Screens

### 1. Onboarding Screens
- Welcome/Splash
- Register
- Login
- Google Sign-in
- Profile Setup
- Permission Requests

### 2. Home Screen
- Quick actions (Send Package, Go Available, Find Travelers)
- Active deliveries summary
- Nearby helpers count
- Recent activity

### 3. Send Package Flow
- Package Details Form
- Pickup Location
- Delivery Location
- Find Travelers (List/Map)
- Traveler Profile
- Chat & Negotiate
- Payment
- Tracking

### 4. Traveler Flow
- Create Trip Form
- My Trips List
- Go Available Toggle
- Incoming Requests
- Chat & Negotiate
- Active Deliveries
- Update Status
- Earnings

### 5. Chat Screen
- Messages List
- Message Input
- Price Proposal Card
- Accept/Counter Buttons
- Package Details Card

### 6. Delivery Tracking
- Map with Route
- Status Timeline
- Contact Buttons
- Proof Photos
- Confirm Delivery

### 7. Profile & Settings
- My Profile
- Edit Profile
- My Reviews
- Payment Methods
- Earnings/Wallet
- Location Settings
- Notification Settings

### 8. Notifications
- Notification List
- Notification Details
- Mark as Read

---

## Testing Checklist

### Authentication
- [ ] Register new user
- [ ] Login with email/password
- [ ] Google Sign-in
- [ ] Token refresh
- [ ] Logout

### User Profile
- [ ] View profile
- [ ] Update profile
- [ ] Upload profile image
- [ ] View other user's profile

### Availability
- [ ] Toggle availability on/off
- [ ] Update location while available
- [ ] Search nearby users
- [ ] Auto-expire after duration

### Trips
- [ ] Create scheduled trip
- [ ] Search trips
- [ ] View trip details
- [ ] Update trip status

### Packages & Deliveries
- [ ] Create package request
- [ ] Search packages
- [ ] Accept delivery
- [ ] Update delivery status
- [ ] Upload proof photos
- [ ] Track delivery location

### Payments
- [ ] Initiate payment
- [ ] Complete payment
- [ ] View payment history
- [ ] View earnings/spending

### Messaging
- [ ] Start conversation
- [ ] Send/receive messages
- [ ] Send price proposal
- [ ] Accept price
- [ ] Real-time updates

### Reviews
- [ ] Leave review
- [ ] View reviews
- [ ] Rating statistics

### Notifications
- [ ] Receive push notifications
- [ ] View notification list
- [ ] Mark as read

---

## API Base URLs

| Environment | Base URL |
|-------------|----------|
| Development | `https://localhost:500X` |
| Staging | `https://staging-api.tagalong.com` |
| Production | `https://api.tagalong.com` |

Note: Each service runs on a different port in development. In production, use API Gateway for single entry point.
