# Enerit: план IPC и генерации invocation

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Планируемые semantics и новые API уточняются перед соответствующим этапом.

## Границы

TrueMoon.Enerit, Enerit.Generator, Enerit.Tests и Enerit.Generator.Tests. Invocation client/server, handler resolution, сериализация, pipes/memory-mapped transports и generated service adapters.

Запуск и перезапуск процесса относится к [Mithril](../mithril/PLAN.md), app lifecycle и общие service contracts — к [Core](../core/PLAN.md), DI registration/resolution — к [Cobalt](../cobalt/PLAN.md). Транспорт не обязан управлять lifetime процесса.

## Цель

Два собственных процесса выполняют типизированный invocation через один заявленный transport, передают поддержанные значения, cancellation и ошибки, а завершение client/server не оставляет каналов или ожиданий. Возможности второго transport подтверждаются отдельно.

## Архитектура и решения

Сохранять IInvocationClient/IInvocationServer, factories/handlers и ISerializer boundaries. Runtime использует Contracts/Core и свой generator. Transport choice остаётся явным, generated adapters должны соответствовать runtime framing/serialization.

До утверждения production readiness определить framing/size limits, method mapping, совместимость payload, cancellation/timeout/disconnect semantics и владение буферами/каналами. Чтение существующих interfaces не означает, что эти гарантии уже выполнены. Pipes и memory-mapped реализации не объявляются равно проверенными.

## Этапы

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| ENE0 | Runtime/generator/transport baseline | Discovery/results существующих projects, активные и отключённые transport scenarios перечислены отдельно |
| ENE1 | Serialization и generated mapping contract | Supported values/null/collections и malformed/unsupported payloads имеют проверяемый результат; generated code компилируется и согласован с runtime |
| ENE2 | Один работоспособный IPC transport | Own-process invocation, concurrent requests в выбранном режиме, cancellation/disconnect/error/cleanup подтверждены с фиксированным transport |
| ENE3 | Второй transport и сопоставление capabilities | Общий сценарий повторён, transport-specific limits/failures и ownership задокументированы; неподтверждённый режим остаётся experimental |
| ENE4 | Совместный scenario с Mithril | Unit выполняет invocation, отказывает/завершается, согласованный client/server cleanup и дальнейшее поведение воспроизводятся |
| ENE5 | Package consumer и baseline | Generator/runtime/transport доставлены независимому consumer; измерены latency/throughput/allocations выбранного сценария до оптимизаций |

Начать ENE2 с transport, подтвердившего ENE0; не назначать его успешным только из-за наличия реализации. ENE4 зависит от MIT1–MIT3 и согласованной boundary между channel lifecycle и restart process.

## Критерии готовности

- Typed invocation и serialization соответствуют заявленному API; generation/diagnostics и unsupported values проверены.
- Cancellation, remote error, disconnect, invalid/oversized payload и shutdown имеют согласованный результат без зависших ожиданий.
- Client/server/channel/buffer ownership и пределы ресурсов описаны и проверены.
- Заявленные transports проверены независимо; disabled/commented tests не выдаются за successful run.
- Separate-process и package consumers воспроизводимы, generator delivery проверен вне ProjectReference.

## После первой рабочей проверки

Развивать payload compatibility, diagnostics и throughput по реальному consumer. Reconnection/backpressure и новые transports требуют отдельного contract proposal; сетевой RPC этим планом не обещается.
