# Changelog

## [0.2.0](https://github.com/equinor/EtpClient/compare/v0.1.3...v0.2.0) (2026-05-19)


### ⚠ BREAKING CHANGES

* **channel-streaming:** RequestChannelRangeAsync return type changed from Task<ChannelRangeResult> to IAsyncEnumerable<ChannelDataItem>. ChannelRangeResult and ChannelRangeResultState have been removed. SampleRunOutcome.ChannelRangeResult replaced by RangeRequest and RangeSamples properties.

### Features

* **channel-streaming:** Stream RequestChannelRangeAsync as IAsyncEn… ([#13](https://github.com/equinor/EtpClient/issues/13)) ([778f72c](https://github.com/equinor/EtpClient/commit/778f72ca56e4aeec55893f6b47110a48f62443cc))

## [0.1.3](https://github.com/equinor/EtpClient/compare/v0.1.2...v0.1.3) (2026-05-05)


### Features

* **instrumentation:** Add OpenTelemetry tracing and metrics ([#10](https://github.com/equinor/EtpClient/issues/10)) ([6b7f92c](https://github.com/equinor/EtpClient/commit/6b7f92cfb257adc10fa7985d29ba69bb549d2001))

## [0.1.2](https://github.com/equinor/EtpClient/compare/v0.1.1...v0.1.2) (2026-04-17)


### Features

* Add IEtpClient interface ([#8](https://github.com/equinor/EtpClient/issues/8)) ([75ace9d](https://github.com/equinor/EtpClient/commit/75ace9db70fd804cfc5cc9537d6acd39004b2381))

## [0.1.1](https://github.com/equinor/EtpClient/compare/v0.1.0...v0.1.1) (2026-04-16)


### Features

* **explorer:** add active-column search and filter (feature 008) ([#2](https://github.com/equinor/EtpClient/issues/2)) ([d70a270](https://github.com/equinor/EtpClient/commit/d70a27039351c75d17401fff856897b8024b85e1))
* **explorer:** fixed-row streaming list with in-place updates ([#3](https://github.com/equinor/EtpClient/issues/3)) ([1bfdf4c](https://github.com/equinor/EtpClient/commit/1bfdf4c73faa0847469528154f1e670763f3c4ee))
* **specify:** add git extension and ETP templates ([1dbf208](https://github.com/equinor/EtpClient/commit/1dbf20800bea6ad4c7b6478b224c57c3f98168c4))
