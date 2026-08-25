const form = document.querySelector('#measurement-form');
const scenarioInput = document.querySelector('#scenario');
const quantityInput = document.querySelector('#quantity');
const repetitionsInput = document.querySelector('#repetitions');
const warmupInput = document.querySelector('#warmup');
const forceGcInput = document.querySelector('#force-gc');
const measureButton = document.querySelector('#measure-button');
const downloadButton = document.querySelector('#download-button');
const requestStatus = document.querySelector('#request-status');
const scenarioDescription = document.querySelector('#scenario-description');
const scenarioCards = document.querySelector('#scenario-cards');
const resultsSection = document.querySelector('#results');
const emptyHistory = document.querySelector('#empty-history');
const historyContent = document.querySelector('#history-content');
const historyBars = document.querySelector('#history-bars');
const historyTable = document.querySelector('#history-table');
const queryForm = document.querySelector('#query-measurement-form');
const queryScenarioInput = document.querySelector('#query-scenario');
const queryQuantityInput = document.querySelector('#query-quantity');
const queryRepetitionsInput = document.querySelector('#query-repetitions');
const queryWarmupInput = document.querySelector('#query-warmup');
const queryForceGcInput = document.querySelector('#query-force-gc');
const queryMeasureButton = document.querySelector('#query-measure-button');
const queryRequestStatus = document.querySelector('#query-request-status');
const queryScenarioDescription = document.querySelector('#query-scenario-description');
const queryScenarioCards = document.querySelector('#query-scenario-cards');
const queryResults = document.querySelector('#query-results');
const queryHistoryBars = document.querySelector('#query-history-bars');
const queryEmptyHistory = document.querySelector('#query-empty-history');
const queryHistoryContent = document.querySelector('#query-history-content');
const translationBadge = document.querySelector('#translation-badge');
const translationMessage = document.querySelector('#translation-message');

const history = [];
const scenarioMetadata = new Map();
const queryHistory = [];
const queryScenarioMetadata = new Map();

const scenarioLabels = {
    'atual': 'Atual',
    'xssf-sem-to-array': 'XSSF sem ToArray',
    'sxssf-com-lista': 'SXSSF com lista',
    'sxssf-stream-arquivo': 'SXSSF + arquivo',
    'sxssf-stream-response': 'SXSSF + response'
};

const queryScenarioLabels = {
    'bufferizado-cliente': 'Bufferizado no cliente',
    'streaming-cliente': 'EF streaming + cliente',
    'streaming-sql-case': 'EF streaming + SQL CASE',
    'dbreader-direto': 'DbReader direto',
    'dbreader-processado': 'DbReader processado'
};

const numberFormatter = new Intl.NumberFormat('pt-BR', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2
});

const integerFormatter = new Intl.NumberFormat('pt-BR', {
    maximumFractionDigits: 0
});

async function loadScenarios() {
    try {
        const response = await fetch('/api/exportacoes/estoque/cenarios');

        if (!response.ok) {
            throw new Error('Não foi possível carregar os cenários.');
        }

        const scenarios = await response.json();
        const selectedValue = scenarioInput.value;
        scenarioInput.replaceChildren();

        scenarios.forEach((scenario) => {
            scenarioMetadata.set(scenario.route, scenario);

            const option = document.createElement('option');
            option.value = scenario.route;
            option.textContent = scenarioLabels[scenario.route] ?? scenario.route;
            scenarioInput.append(option);
        });

        scenarioInput.value = selectedValue;
        renderScenarioGuide(scenarios);
        updateScenarioDescription();
    } catch (error) {
        setStatus(error.message, true);
    }
}

function updateScenarioDescription() {
    const metadata = scenarioMetadata.get(scenarioInput.value);

    if (metadata) {
        scenarioDescription.textContent = metadata.objective;
    }

    updateScenarioCardSelection();
}

function renderScenarioGuide(scenarios) {
    scenarioCards.replaceChildren();

    scenarios.forEach((scenario, index) => {
        const card = document.createElement('button');
        card.type = 'button';
        card.className = 'scenario-card';
        card.dataset.scenario = scenario.route;

        const top = document.createElement('div');
        top.className = 'scenario-card-top';

        const title = document.createElement('h4');
        title.textContent = scenarioLabels[scenario.route] ?? scenario.route;

        const number = document.createElement('span');
        number.className = 'scenario-card-number';
        number.textContent = String(index + 1).padStart(2, '0');
        top.append(title, number);

        const description = document.createElement('p');
        description.className = 'scenario-card-description';
        description.textContent = scenario.objective;

        const flow = document.createElement('div');
        flow.className = 'scenario-flow';

        [scenario.dataSource, scenario.workbook, scenario.target].forEach((step) => {
            const item = document.createElement('span');
            item.textContent = step;
            flow.append(item);
        });

        card.append(top, description, flow);
        card.addEventListener('click', () => {
            scenarioInput.value = scenario.route;
            updateScenarioDescription();
            scenarioInput.focus();
        });

        scenarioCards.append(card);
    });

    updateScenarioCardSelection();
}

function updateScenarioCardSelection() {
    scenarioCards.querySelectorAll('[data-scenario]').forEach((card) => {
        const isSelected = card.dataset.scenario === scenarioInput.value;
        card.classList.toggle('active', isSelected);
        card.setAttribute('aria-pressed', String(isSelected));
    });
}

async function executeMeasurement(event) {
    event.preventDefault();

    if (!form.reportValidity()) {
        return;
    }

    const scenario = scenarioInput.value;
    const quantity = Number(quantityInput.value);
    const repetitions = Number(repetitionsInput.value);
    const query = new URLSearchParams({
        quantidade: String(quantity),
        repeticoes: String(repetitions),
        aquecer: String(warmupInput.checked),
        forcarGc: String(forceGcInput.checked)
    });

    setLoading(true);
    setStatus(
        `Executando ${scenarioLabels[scenario] ?? scenario}: ${repetitions} medições válidas` +
        `${warmupInput.checked ? ' + 1 aquecimento' : ''}...`
    );

    try {
        const response = await fetch(`/api/medicoes/estoque/${encodeURIComponent(scenario)}?${query}`, {
            method: 'POST',
            headers: { 'Accept': 'application/json' }
        });

        if (!response.ok) {
            throw new Error(await readError(response));
        }

        const result = await response.json();
        renderResult(result);
        addToHistory(result);
        setStatus(
            `Benchmark concluído: mediana de ${result.repetitions} execuções em ` +
            `${formatMilliseconds(result.statistics.durationMs.median)}.`
        );
        resultsSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (error) {
        setStatus(error.message || 'A medição falhou.', true);
    } finally {
        setLoading(false);
    }
}

async function readError(response) {
    try {
        const problem = await response.json();

        if (problem.errors) {
            return Object.values(problem.errors).flat().join(' ');
        }

        return problem.message || problem.detail || problem.title || `Erro HTTP ${response.status}.`;
    } catch {
        return `Erro HTTP ${response.status}.`;
    }
}

async function loadQueryBenchmark() {
    await Promise.all([loadQueryScenarios(), loadTranslationDiagnostic()]);
}

async function loadQueryScenarios() {
    try {
        const response = await fetch('/api/benchmarks/query-miniexcel/cenarios');

        if (!response.ok) {
            throw new Error('Não foi possível carregar os cenários EF Core + MiniExcel.');
        }

        const scenarios = await response.json();
        queryScenarioInput.replaceChildren();

        scenarios.forEach((scenario) => {
            queryScenarioMetadata.set(scenario.name, scenario);

            const option = document.createElement('option');
            option.value = scenario.name;
            option.textContent = queryScenarioLabels[scenario.name] ?? scenario.name;
            queryScenarioInput.append(option);
        });

        queryScenarioInput.value = 'dbreader-processado';
        renderQueryScenarioGuide(scenarios);
        updateQueryScenarioDescription();
    } catch (error) {
        setQueryStatus(error.message, true);
    }
}

async function loadTranslationDiagnostic() {
    try {
        const response = await fetch('/api/benchmarks/query-miniexcel/diagnostico');

        if (!response.ok) {
            throw new Error('O diagnóstico de tradução não respondeu.');
        }

        const diagnostic = await response.json();
        translationBadge.classList.remove('loading');

        if (diagnostic.translatable) {
            translationBadge.textContent = 'traduzível';
            translationMessage.textContent = diagnostic.message;
        } else {
            translationBadge.textContent = 'falha esperada';
            translationBadge.classList.add('expected');
            translationMessage.textContent = diagnostic.recommendedSolution;
            translationMessage.title = diagnostic.message;
        }
    } catch (error) {
        translationBadge.classList.remove('loading');
        translationBadge.textContent = 'indisponível';
        translationMessage.textContent = error.message;
    }
}

function renderQueryScenarioGuide(scenarios) {
    queryScenarioCards.replaceChildren();

    scenarios.forEach((scenario, index) => {
        const card = document.createElement('button');
        card.type = 'button';
        card.className = 'scenario-card';
        card.dataset.queryScenario = scenario.name;

        const top = document.createElement('div');
        top.className = 'scenario-card-top';

        const title = document.createElement('h4');
        title.textContent = queryScenarioLabels[scenario.name] ?? scenario.name;

        const number = document.createElement('span');
        number.className = 'scenario-card-number';
        number.textContent = String(index + 1).padStart(2, '0');
        top.append(title, number);

        const description = document.createElement('p');
        description.className = 'scenario-card-description';
        description.textContent = scenario.objective;

        const flow = document.createElement('div');
        flow.className = 'scenario-flow';

        [scenario.query, scenario.enumConversion, scenario.materialization].forEach((step) => {
            const item = document.createElement('span');
            item.textContent = step;
            flow.append(item);
        });

        card.append(top, description, flow);
        card.addEventListener('click', () => {
            queryScenarioInput.value = scenario.name;
            updateQueryScenarioDescription();
            queryScenarioInput.focus();
        });
        queryScenarioCards.append(card);
    });

    updateQueryScenarioSelection();
}

function updateQueryScenarioDescription() {
    const metadata = queryScenarioMetadata.get(queryScenarioInput.value);

    if (metadata) {
        queryScenarioDescription.textContent = metadata.objective;
    }

    updateQueryScenarioSelection();
}

function updateQueryScenarioSelection() {
    queryScenarioCards.querySelectorAll('[data-query-scenario]').forEach((card) => {
        const isSelected = card.dataset.queryScenario === queryScenarioInput.value;
        card.classList.toggle('active', isSelected);
        card.setAttribute('aria-pressed', String(isSelected));
    });
}

async function executeQueryMeasurement(event) {
    event.preventDefault();

    if (!queryForm.reportValidity()) {
        return;
    }

    const scenario = queryScenarioInput.value;
    const quantity = Number(queryQuantityInput.value);
    const repetitions = Number(queryRepetitionsInput.value);
    const query = new URLSearchParams({
        quantidade: String(quantity),
        repeticoes: String(repetitions),
        aquecer: String(queryWarmupInput.checked),
        forcarGc: String(queryForceGcInput.checked)
    });

    const executionLabel = queryScenarioLabels[scenario] ?? scenario;
    const startedAt = performance.now();
    setQueryLoading(true);
    setQueryStatus(
        `Executando ${executionLabel}: ${repetitions} medições válidas` +
        `${queryWarmupInput.checked ? ' + 1 aquecimento' : ''}…`
    );

    const progressTimer = window.setInterval(() => {
        const elapsedSeconds = Math.floor((performance.now() - startedAt) / 1000);
        setQueryStatus(
            `Executando ${executionLabel}: ${repetitions} medições válidas… ${elapsedSeconds}s. ` +
            'O resultado final usará a mediana e mostrará a faixa observada.'
        );
    }, 1000);

    try {
        const response = await fetch(
            `/api/benchmarks/query-miniexcel/${encodeURIComponent(scenario)}?${query}`,
            { method: 'POST', headers: { 'Accept': 'application/json' } }
        );

        if (!response.ok) {
            throw new Error(await readError(response));
        }

        const result = await response.json();
        renderQueryResult(result);
        addToQueryHistory(result);
        setQueryStatus(
            `Benchmark concluído: mediana de ${result.repetitions} execuções em ` +
            `${formatMilliseconds(result.statistics.durationMs.median)}.`
        );
        queryResults.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (error) {
        setQueryStatus(error.message || 'A medição da consulta falhou.', true);
    } finally {
        window.clearInterval(progressTimer);
        setQueryLoading(false);
    }
}

function renderQueryResult(result) {
    const statistics = result.statistics;

    queryResults.classList.remove('hidden');
    document.querySelector('#query-result-title').textContent =
        `${queryScenarioLabels[result.scenario] ?? result.scenario} · ${integerFormatter.format(result.quantity)} linhas`;
    document.querySelector('#query-allocated').textContent = formatMiB(statistics.allocatedMemoryMiB.median);
    document.querySelector('#query-allocated-range').textContent = formatMetricRange(
        statistics.allocatedMemoryMiB,
        formatMiB
    );
    document.querySelector('#query-managed-peak').textContent = formatMiB(statistics.sampledManagedPeakMiB.median);
    document.querySelector('#query-managed-range').textContent = formatMetricRange(
        statistics.sampledManagedPeakMiB,
        formatMiB
    );
    document.querySelector('#query-working-set').textContent = formatMiB(statistics.sampledWorkingSetPeakMiB.median);
    document.querySelector('#query-working-set-range').textContent = formatMetricRange(
        statistics.sampledWorkingSetPeakMiB,
        formatMiB
    );
    document.querySelector('#query-duration').textContent = formatMilliseconds(statistics.durationMs.median);
    document.querySelector('#query-duration-range').textContent = formatMetricRange(
        statistics.durationMs,
        formatMilliseconds
    );
    document.querySelector('#query-file-size').textContent = formatMiB(statistics.fileSizeMiB.median);
    document.querySelector('#query-sql').textContent = result.generatedSql;

    const flags = document.querySelector('#query-result-flags');
    flags.replaceChildren();
    appendResultFlag(flags, result.buffersResults ? 'bufferiza tudo' : 'streaming', !result.buffersResults);
    appendResultFlag(
        flags,
        result.clientSideEnumConversion ? 'enum no cliente' : 'enum no SQL',
        !result.clientSideEnumConversion
    );
    appendResultFlag(flags, `${result.repetitions} medições válidas`, true);
    appendResultFlag(
        flags,
        result.warmUpRunDiscarded ? 'aquecimento descartado' : 'sem aquecimento',
        result.warmUpRunDiscarded
    );
    appendResultFlag(flags, `pico amostrado a cada ${result.samplingIntervalMs} ms`, false);
}

function appendResultFlag(container, text, isPositive) {
    const flag = document.createElement('span');
    flag.textContent = text;
    flag.classList.toggle('positive', isPositive);
    container.append(flag);
}

function addToQueryHistory(result) {
    queryHistory.unshift(result);

    if (queryHistory.length > 10) {
        queryHistory.pop();
    }

    saveQueryHistory();
    renderQueryHistory();
}

function renderQueryHistory() {
    if (queryHistory.length === 0) {
        queryEmptyHistory.classList.remove('hidden');
        queryHistoryContent.classList.add('hidden');
        return;
    }

    queryEmptyHistory.classList.add('hidden');
    queryHistoryContent.classList.remove('hidden');
    const maxValue = Math.max(
        ...queryHistory.map((item) => item.statistics.allocatedMemoryMiB.median),
        1
    );
    queryHistoryBars.replaceChildren();

    queryHistory.forEach((item) => {
        const row = document.createElement('div');
        row.className = 'history-bar';

        const label = document.createElement('span');
        label.className = 'history-bar-label';
        label.textContent = `${queryScenarioLabels[item.scenario] ?? item.scenario} · ${integerFormatter.format(item.quantity)}`;

        const track = document.createElement('div');
        track.className = 'history-bar-track';
        const fill = document.createElement('span');
        const allocatedMemory = item.statistics.allocatedMemoryMiB;
        fill.style.width = `${Math.max(3, allocatedMemory.median / maxValue * 100)}%`;
        track.append(fill);

        const value = document.createElement('span');
        value.className = 'history-bar-value';
        value.textContent = formatMiB(allocatedMemory.median);
        row.append(label, track, value);
        queryHistoryBars.append(row);
    });
}

function saveQueryHistory() {
    try {
        const summaries = queryHistory.map((item) => ({
            scenario: item.scenario,
            quantity: item.quantity,
            repetitions: item.repetitions,
            statistics: {
                allocatedMemoryMiB: item.statistics.allocatedMemoryMiB
            }
        }));
        sessionStorage.setItem('query-miniexcel-history-v3', JSON.stringify(summaries));
    } catch {
        // O histórico continua funcionando em memória quando o storage não está disponível.
    }
}

function restoreQueryHistory() {
    try {
        const saved = JSON.parse(sessionStorage.getItem('query-miniexcel-history-v3') ?? '[]');

        if (Array.isArray(saved)) {
            queryHistory.push(...saved.slice(0, 10));
        }
    } catch {
        // Ignora conteúdo inválido ou storage indisponível.
    }

    renderQueryHistory();
}

function setQueryLoading(isLoading) {
    queryMeasureButton.disabled = isLoading;
    measureButton.disabled = isLoading;
    downloadButton.disabled = isLoading;
    queryMeasureButton.classList.toggle('is-loading', isLoading);
}

function setQueryStatus(message, isError = false) {
    queryRequestStatus.textContent = message;
    queryRequestStatus.classList.toggle('error', isError);
}

function renderResult(result) {
    const statistics = result.statistics;

    resultsSection.classList.remove('hidden');
    document.querySelector('#result-scenario').textContent = scenarioLabels[result.scenario] ?? result.scenario;
    document.querySelector('#allocated-memory').textContent = formatMiB(statistics.allocatedMemoryMiB.median);
    document.querySelector('#allocated-range').textContent = formatMetricRange(
        statistics.allocatedMemoryMiB,
        formatMiB
    );
    document.querySelector('#managed-peak').textContent = formatMiB(statistics.sampledManagedPeakMiB.median);
    document.querySelector('#managed-range').textContent =
        `${formatMetricRange(statistics.sampledManagedPeakMiB, formatMiB)} · amostragem de ${result.samplingIntervalMs} ms`;
    document.querySelector('#working-set').textContent = formatMiB(statistics.sampledWorkingSetPeakMiB.median);
    document.querySelector('#working-set-range').textContent = formatMetricRange(
        statistics.sampledWorkingSetPeakMiB,
        formatMiB
    );
    document.querySelector('#private-memory').textContent = formatMiB(statistics.sampledPrivateMemoryPeakMiB.median);
    document.querySelector('#private-memory-range').textContent = formatMetricRange(
        statistics.sampledPrivateMemoryPeakMiB,
        formatMiB
    );
    document.querySelector('#duration').textContent = formatMilliseconds(statistics.durationMs.median);
    document.querySelector('#duration-range').textContent = formatMetricRange(
        statistics.durationMs,
        formatMilliseconds
    );
    document.querySelector('#file-size').textContent = formatMiB(statistics.fileSizeMiB.median);
    document.querySelector('#measurement-target').textContent = result.measurementTarget;
    document.querySelector('#benchmark-series').textContent =
        `${result.repetitions} válidas${result.warmUpRunDiscarded ? ' + 1 aquecimento' : ''}`;

    const stability = statistics.allocatedMemoryMiB.maximum > 0
        ? statistics.allocatedMemoryMiB.minimum / statistics.allocatedMemoryMiB.maximum * 100
        : 100;
    document.querySelector('#allocated-track').style.width = `${Math.max(8, stability)}%`;
}

function addToHistory(result) {
    history.unshift(result);

    if (history.length > 8) {
        history.pop();
    }

    emptyHistory.classList.add('hidden');
    historyContent.classList.remove('hidden');
    renderHistoryBars();
    renderHistoryTable();
}

function renderHistoryBars() {
    const maxValue = Math.max(
        ...history.map((item) => item.statistics.allocatedMemoryMiB.median),
        1
    );
    historyBars.replaceChildren();

    history.forEach((item) => {
        const row = document.createElement('div');
        row.className = 'history-bar';

        const label = document.createElement('span');
        label.className = 'history-bar-label';
        label.textContent = scenarioLabels[item.scenario] ?? item.scenario;

        const track = document.createElement('div');
        track.className = 'history-bar-track';
        const fill = document.createElement('span');
        const allocatedMemory = item.statistics.allocatedMemoryMiB;
        fill.style.width = `${Math.max(3, allocatedMemory.median / maxValue * 100)}%`;
        track.append(fill);

        const value = document.createElement('span');
        value.className = 'history-bar-value';
        value.textContent = formatMiB(allocatedMemory.median);

        row.append(label, track, value);
        historyBars.append(row);
    });
}

function renderHistoryTable() {
    historyTable.replaceChildren();

    history.forEach((item) => {
        const row = document.createElement('tr');
        const values = [
            scenarioLabels[item.scenario] ?? item.scenario,
            integerFormatter.format(item.quantity),
            formatMiB(item.statistics.allocatedMemoryMiB.median),
            formatMiB(item.statistics.sampledManagedPeakMiB.median),
            formatMilliseconds(item.statistics.durationMs.median)
        ];

        values.forEach((value) => {
            const cell = document.createElement('td');
            cell.textContent = value;
            row.append(cell);
        });

        historyTable.append(row);
    });
}

function setLoading(isLoading) {
    measureButton.disabled = isLoading;
    downloadButton.disabled = isLoading;
    queryMeasureButton.disabled = isLoading;
    measureButton.classList.toggle('is-loading', isLoading);
}

function setStatus(message, isError = false) {
    requestStatus.textContent = message;
    requestStatus.classList.toggle('error', isError);
}

function formatMiB(value) {
    return `${numberFormatter.format(value)} MiB`;
}

function formatMilliseconds(value) {
    return `${numberFormatter.format(value)} ms`;
}

function formatMetricRange(metric, formatter) {
    return `mín. ${formatter(metric.minimum)} · máx. ${formatter(metric.maximum)}`;
}

form.addEventListener('submit', executeMeasurement);
scenarioInput.addEventListener('change', updateScenarioDescription);

downloadButton.addEventListener('click', () => {
    if (!form.reportValidity()) {
        return;
    }

    const scenario = encodeURIComponent(scenarioInput.value);
    const quantity = encodeURIComponent(quantityInput.value);
    window.location.assign(`/api/exportacoes/estoque/${scenario}?quantidade=${quantity}`);
});

document.querySelectorAll('[data-quantity]').forEach((button) => {
    button.addEventListener('click', () => {
        quantityInput.value = button.dataset.quantity;
        document.querySelectorAll('[data-quantity]').forEach((item) => item.classList.remove('active'));
        button.classList.add('active');
    });
});

quantityInput.addEventListener('input', () => {
    document.querySelectorAll('[data-quantity]').forEach((button) => {
        button.classList.toggle('active', button.dataset.quantity === quantityInput.value);
    });
});

loadScenarios();
queryForm.addEventListener('submit', executeQueryMeasurement);
queryScenarioInput.addEventListener('change', updateQueryScenarioDescription);

document.querySelectorAll('[data-query-quantity]').forEach((button) => {
    button.addEventListener('click', () => {
        queryQuantityInput.value = button.dataset.queryQuantity;
        document.querySelectorAll('[data-query-quantity]').forEach((item) => item.classList.remove('active'));
        button.classList.add('active');
    });
});

queryQuantityInput.addEventListener('input', () => {
    document.querySelectorAll('[data-query-quantity]').forEach((button) => {
        button.classList.toggle('active', button.dataset.queryQuantity === queryQuantityInput.value);
    });
});

loadQueryBenchmark();
restoreQueryHistory();
