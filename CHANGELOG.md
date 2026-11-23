# Changelog

## [Unreleased]

### Fixed

- A request with a 33rd header line is refused, as the header cap intends.
- A request that arrives after the overlay is disposed no longer throws ObjectDisposedException.

## 0.1.0

- Serves Mortar's stream overlay for Stardew Valley, out of the Mortar SMAPI Bridge.
