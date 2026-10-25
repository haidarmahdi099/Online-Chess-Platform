import * as signalR from "@microsoft/signalr";
import { apiUrl } from "./settings.js";

export function createGameConnection(accessToken) {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${apiUrl}/hubs/game`, {
      accessTokenFactory: () => accessToken,
      transport: signalR.HttpTransportType.WebSockets,
      skipNegotiation: true,
    })
    .configureLogging(signalR.LogLevel.Warning)
    .withAutomaticReconnect()
    .build();
}

export function waitForHubEvent(connection, eventName, predicate = () => true, timeoutMs = 10_000) {
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => finish(new Error(`Timed out waiting for SignalR event '${eventName}'.`)), timeoutMs);
    const handler = (payload) => {
      if (predicate(payload)) finish(undefined, payload);
    };

    function finish(error, value) {
      clearTimeout(timeout);
      connection.off(eventName, handler);
      if (error) reject(error);
      else resolve(value);
    }

    connection.on(eventName, handler);
  });
}
