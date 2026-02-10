# TagAlong Availability & Location Feature - Mobile API Documentation

## Overview

This feature allows users to:
1. **Toggle Availability** - Mark themselves as "available for errands"
2. **Real-time Location Sharing** - Share current location while available
3. **Discover Nearby Users** - Find available users based on distance
4. **Real-time Updates** - Get live notifications when users go online/offline nearby

---

## Authentication

All endpoints require JWT Bearer token authentication.

```
Authorization: Bearer <jwt_token>
```

Get the token from the Identity service login endpoint.

---

## REST API Endpoints

Base URL: `https://your-server/api/users/availability`

### 1. Get My Availability Status

**Endpoint:** `GET /me`

**Description:** Get current user's availability status and location info.

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

**UI Usage:**
- Show toggle state (on/off)
- Display time remaining until auto-expire
- Show current location name

---

### 2. Set Availability (Toggle On/Off)

**Endpoint:** `POST /me`

**Description:** Turn availability on or off. When turning on, location is required.

**Request Body (Turn ON):**
```json
{
  "isAvailable": true,
  "latitude": 6.5244,
  "longitude": 3.3792,
  "locationName": "Victoria Island, Lagos",
  "durationMinutes": 240
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `isAvailable` | boolean | Yes | `true` to go online, `false` to go offline |
| `latitude` | number | When ON | GPS latitude (-90 to 90) |
| `longitude` | number | When ON | GPS longitude (-180 to 180) |
| `locationName` | string | No | Human-readable address (from geocoding) |
| `durationMinutes` | integer | No | Auto-expire time (default: 240 = 4 hours, max: 480 = 8 hours) |

**Request Body (Turn OFF):**
```json
{
  "isAvailable": false
}
```

**Response:** Same as GET /me

**UI Usage:**
- "Go Available" / "Go Offline" toggle button
- Duration picker (1hr, 2hr, 4hr, 8hr options)
- Request GPS permission before turning on

**Error Responses:**
| Code | Message | Reason |
|------|---------|--------|
| 400 | "Location sharing is disabled" | User disabled location sharing in preferences |
| 400 | "Only verified users can set availability" | User not verified yet |
| 400 | "Location required when setting available" | Missing lat/lon when isAvailable=true |

---

### 3. Update Current Location

**Endpoint:** `PUT /me/location`

**Description:** Update location while available. Call this periodically (every 30-60 seconds).

**Request Body:**
```json
{
  "latitude": 6.5250,
  "longitude": 3.3800,
  "locationName": "Lekki Phase 1, Lagos"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `latitude` | number | Yes | GPS latitude |
| `longitude` | number | Yes | GPS longitude |
| `locationName` | string | No | Updated address |

**Response:**
```json
{
  "latitude": 6.5250,
  "longitude": 3.3800,
  "locationName": "Lekki Phase 1, Lagos",
  "updatedAt": "2026-02-06T10:30:00Z"
}
```

**UI Usage:**
- Call this in background while user is available
- Use device's background location service
- Only call if user moved > 50 meters (to save battery)

---

### 4. Update Location Preferences

**Endpoint:** `PUT /me/preferences`

**Description:** Update user's location sharing preferences.

**Request Body:**
```json
{
  "maxTravelRadiusKm": 15.0,
  "allowLocationSharing": true
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `maxTravelRadiusKm` | number | Yes | How far user is willing to travel (1-50 km) |
| `allowLocationSharing` | boolean | Yes | Master toggle for location features |

**Response:**
```json
{
  "maxTravelRadiusKm": 15.0,
  "allowLocationSharing": true
}
```

**UI Usage:**
- Settings screen with slider for radius
- Privacy toggle for location sharing

---

### 5. Search Nearby Available Users

**Endpoint:** `GET /nearby`

**Description:** Find available users within a radius of given coordinates.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `latitude` | number | Yes | Search center latitude |
| `longitude` | number | Yes | Search center longitude |
| `radiusKm` | number | No | Search radius in km (default: 10, max: 50) |
| `page` | integer | No | Page number (default: 1) |
| `pageSize` | integer | No | Results per page (default: 20, max: 50) |

**Example:** `GET /nearby?latitude=6.5244&longitude=3.3792&radiusKm=5&page=1&pageSize=20`

**Response:**
```json
{
  "users": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440001",
      "firstName": "John",
      "lastName": "Doe",
      "profileImageUrl": "https://storage.example.com/profiles/john.jpg",
      "averageRating": 4.8,
      "totalRatings": 25,
      "completedDeliveries": 42,
      "completedTrips": 15,
      "isVerified": true,
      "distanceKm": 1.2,
      "locationName": "Ikeja, Lagos",
      "locationUpdatedAt": "2026-02-06T10:25:00Z"
    },
    {
      "id": "550e8400-e29b-41d4-a716-446655440002",
      "firstName": "Jane",
      "lastName": "Smith",
      "profileImageUrl": null,
      "averageRating": 4.5,
      "totalRatings": 12,
      "completedDeliveries": 18,
      "completedTrips": 8,
      "isVerified": true,
      "distanceKm": 2.5,
      "locationName": "Maryland, Lagos",
      "locationUpdatedAt": "2026-02-06T10:20:00Z"
    }
  ],
  "totalCount": 8,
  "page": 1,
  "pageSize": 20
}
```

**UI Usage:**
- List view of available users sorted by distance
- Show profile picture, name, rating, distance
- Pull-to-refresh to update list
- Map view with pins for each user

---

### 6. Get Nearby Available Count

**Endpoint:** `GET /nearby/count`

**Description:** Get count of available users nearby (for badge/notification).

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `latitude` | number | Yes | Search center latitude |
| `longitude` | number | Yes | Search center longitude |
| `radiusKm` | number | No | Search radius in km (default: 10) |

**Response:**
```json
{
  "count": 8
}
```

**UI Usage:**
- Badge on "Find Helpers" tab
- "8 helpers nearby" indicator

---

## SignalR Real-time Connection

**Hub URL:** `wss://your-server/locationHub?access_token=<jwt_token>`

### Connecting (Mobile Code Example)

```dart
// Flutter example with signalr_netcore package
final hubConnection = HubConnectionBuilder()
    .withUrl(
      'https://your-server/locationHub',
      options: HttpConnectionOptions(
        accessTokenFactory: () async => await getJwtToken(),
      ),
    )
    .withAutomaticReconnect()
    .build();

await hubConnection.start();
```

---

### Hub Methods (App Calls Server)

#### 1. UpdateLocation
Update position while available. Call every 30-60 seconds.

```dart
await hubConnection.invoke('UpdateLocation', args: [
  6.5244,      // latitude
  3.3792,      // longitude
  'Lagos'      // locationName (optional)
]);
```

#### 2. SetAvailable
Go online with location.

```dart
await hubConnection.invoke('SetAvailable', args: [
  6.5244,      // latitude
  3.3792,      // longitude
  'Lagos',     // locationName (optional)
  240          // durationMinutes (optional, default 240)
]);
```

#### 3. SetUnavailable
Go offline.

```dart
await hubConnection.invoke('SetUnavailable');
```

#### 4. SubscribeToNearbyUsers
Start receiving updates about nearby users.

```dart
await hubConnection.invoke('SubscribeToNearbyUsers', args: [
  6.5244,      // latitude
  3.3792,      // longitude
  10.0         // radiusKm
]);
```

#### 5. UnsubscribeFromNearbyUsers
Stop receiving updates.

```dart
await hubConnection.invoke('UnsubscribeFromNearbyUsers', args: [
  6.5244,      // latitude
  3.3792       // longitude
]);
```

---

### Hub Events (Server Sends to App)

#### 1. ReceiveLocationUpdate
Someone nearby moved.

```dart
hubConnection.on('ReceiveLocationUpdate', (arguments) {
  final update = arguments[0];
  // update = {
  //   "userId": "guid",
  //   "latitude": 6.5244,
  //   "longitude": 3.3792,
  //   "locationName": "Lagos",
  //   "updatedAt": "2026-02-06T10:30:00Z"
  // }
});
```

**UI Action:** Update user's pin on map

#### 2. UserBecameAvailable
Someone went online nearby.

```dart
hubConnection.on('UserBecameAvailable', (arguments) {
  final user = arguments[0];
  // user = {
  //   "id": "guid",
  //   "firstName": "John",
  //   "lastName": "Doe",
  //   "profileImageUrl": "https://...",
  //   "averageRating": 4.8,
  //   "completedDeliveries": 42,
  //   "isVerified": true,
  //   "distanceKm": 1.2,
  //   "locationName": "Lagos"
  // }
});
```

**UI Action:** Add new user to list/map, show notification

#### 3. UserBecameUnavailable
Someone went offline.

```dart
hubConnection.on('UserBecameUnavailable', (arguments) {
  final userId = arguments[0]; // Guid string
});
```

**UI Action:** Remove user from list/map

#### 4. NearbyUsersUpdated
Initial list after subscribing.

```dart
hubConnection.on('NearbyUsersUpdated', (arguments) {
  final users = arguments[0]; // Array of user objects
});
```

**UI Action:** Populate initial list/map

#### 5. AvailabilityStatusChanged
Your own status changed (on connect or after toggle).

```dart
hubConnection.on('AvailabilityStatusChanged', (arguments) {
  final status = arguments[0];
  // status = {
  //   "isAvailable": true,
  //   "expiresAt": "2026-02-06T14:00:00Z"
  // }
});
```

**UI Action:** Update toggle state, show expiry countdown

---

## Suggested Mobile UI Screens

### 1. Home Screen
- **"Go Available" Button** (prominent, large)
  - Shows toggle state
  - When ON: shows countdown timer "Available for 3h 45m"
  - Requires GPS permission

- **"Nearby Helpers" Badge**
  - Shows count: "5 helpers nearby"
  - Tapping opens Nearby Users screen

### 2. Availability Toggle Screen
When user taps "Go Available":

```
+----------------------------------+
|        Go Available              |
+----------------------------------+
|                                  |
|  [Map showing current location]  |
|         * You are here           |
|                                  |
+----------------------------------+
|  How long do you want to be      |
|  available?                      |
|                                  |
|  [ 1 hr ] [ 2 hr ] [4 hr] [ 8 hr]|
|           (4 hr selected)        |
+----------------------------------+
|                                  |
|  [====== GO AVAILABLE ======]    |
|                                  |
+----------------------------------+
```

### 3. Nearby Users Screen (List View)
```
+----------------------------------+
|  < Back     Nearby Helpers       |
+----------------------------------+
|  [Map] [List]                    |
+----------------------------------+
|  +---+                           |
|  |   | John D.          1.2 km   |
|  |pic| **** (25) | 42 deliveries |
|  +---+ Verified                  |
+----------------------------------+
|  +---+                           |
|  |   | Jane S.          2.5 km   |
|  |pic| **** (12) | 18 deliveries |
|  +---+ Verified                  |
+----------------------------------+
|  +---+                           |
|  |   | Mike O.          3.1 km   |
|  |pic| *** (8)  | 5 deliveries   |
|  +---+                           |
+----------------------------------+
```

### 4. Nearby Users Screen (Map View)
```
+----------------------------------+
|  < Back     Nearby Helpers       |
+----------------------------------+
|  [Map] [List]                    |
+----------------------------------+
|                                  |
|      *John (1.2km)               |
|                                  |
|            [You]                 |
|                *Jane (2.5km)     |
|                                  |
|    *Mike (3.1km)                 |
|                                  |
+----------------------------------+
|  Tap a pin to view profile       |
+----------------------------------+
```

### 5. Settings Screen
```
+----------------------------------+
|  < Back      Location Settings   |
+----------------------------------+
|                                  |
|  Allow Location Sharing          |
|  [===========O]  ON              |
|                                  |
|  When enabled, you can mark      |
|  yourself as available and be    |
|  discovered by nearby users.     |
|                                  |
+----------------------------------+
|                                  |
|  Maximum Travel Distance         |
|  [----*---------] 10 km          |
|                                  |
|  How far you're willing to       |
|  travel for errands.             |
|                                  |
+----------------------------------+
```

### 6. Available State Indicator (Floating)
When user is available, show persistent indicator:

```
+----------------------------------+
|  [Green dot] Available | 3:45:00 |
|  Tap to go offline               |
+----------------------------------+
```

---

## User Flows

### Flow 1: Going Available

```
1. User taps "Go Available" button
2. App checks GPS permission
   - If not granted: Request permission
   - If denied: Show settings prompt
3. App gets current coordinates
4. App shows duration picker
5. User selects duration (default 4hr)
6. App calls POST /api/users/availability/me
7. App connects to SignalR hub
8. App starts background location updates (every 30s)
9. UI shows "Available" state with countdown
```

### Flow 2: Going Offline

```
1. User taps "Go Offline" or countdown expires
2. App calls POST /api/users/availability/me with isAvailable=false
3. App stops background location updates
4. App can keep SignalR connected (for finding others)
5. UI shows "Offline" state
```

### Flow 3: Finding Available Users

```
1. User taps "Find Helpers" or "Nearby" tab
2. App gets current coordinates
3. App calls GET /api/users/availability/nearby
4. App displays list/map of available users
5. App subscribes to SignalR for real-time updates
6. As users come online/offline, list updates automatically
```

### Flow 4: Background Location Updates

```
While user is available:
1. Background service gets GPS every 30 seconds
2. Check if moved > 50 meters from last known position
3. If moved:
   - Via REST: PUT /api/users/availability/me/location
   - Or via SignalR: hubConnection.invoke('UpdateLocation', ...)
4. If app goes to background for > 5 minutes:
   - Consider auto-going offline to save battery
```

---

## Error Handling

| Error | User Message | Action |
|-------|--------------|--------|
| GPS permission denied | "Location access is required to go available" | Open settings |
| GPS unavailable | "Unable to get your location. Please try again" | Retry button |
| Network error | "Connection lost. Reconnecting..." | Auto-retry |
| Not verified | "Complete verification to become available" | Link to verification |
| Location sharing disabled | "Enable location sharing in settings" | Link to settings |

---

## Testing Checklist

- [ ] Can toggle availability on/off
- [ ] Location updates while available
- [ ] Auto-expire after duration
- [ ] See nearby users list
- [ ] See nearby users on map
- [ ] Real-time updates when users go online/offline
- [ ] Works in background (iOS/Android)
- [ ] Handles GPS permission denial gracefully
- [ ] Handles network disconnection
- [ ] Battery usage is acceptable

---

## API Base URLs

| Environment | URL |
|-------------|-----|
| Development | `https://localhost:5001` |
| Staging | `https://staging-api.tagalong.com` |
| Production | `https://api.tagalong.com` |

SignalR Hub: Append `/locationHub` to base URL
