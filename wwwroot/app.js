const form = document.querySelector('#measurement-form');
const scenarioInput = document.querySelector('#scenario');
const quantityInput = document.querySelector('#quantity');
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
            scenarioMetadata.set(scenario.rota, scenario);

            const option = document.createElement('option');
            option.value = scenario.rota;
            option.textContent = scenarioLabels[scenario.rota] ?? scenario.rota;
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
        scenarioDescription.textContent = metadata.objetivo;
    }

    updateScenarioCardSelection();
}

function renderScenarioGuide(scenarios) {
    scenarioCards.replaceChildren();

    scenarios.forEach((scenario, index) => {
        const card = document.createElement('button');
        card.type = 'button';
        card.className = 'scenario-card';
        card.dataset.scenario = scenario.rota;

        const top = document.createElement('div');
        top.className = 'scenario-card-top';

        const title = document.createElement('h4');
        title.textContent = scenarioLabels[scenario.rota] ?? scenario.rota;

        const number = document.createElement('span');
        number.className = 'scenario-card-number';
        number.textContent = String(index + 1).padStart(2, '0');
        top.append(title, number);

        const description = document.createElement('p');
        description.className = 'scenario-card-description';
        description.textContent = scenario.objetivo;

        const flow = document.createElement('div');
        flow.className = 'scenario-flow';

        [scenario.fonteDeDados, scenario.workbook, scenario.destino].forEach((step) => {
            const item = document.createElement('span');
            item.textContent = step;
            flow.append(item);
        });

        card.append(top, description, flow);
        card.addEventListener('click', () => {
            scenarioInput.value = scenario.rota;
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
    const query = new URLSearchParams({
        quantidade: String(quantity),
        aquecer: String(warmupInput.checked),
        forcarGc: String(forceGcInput.checked)
    });

    setLoading(true);
    setStatus(`Executando ${scenarioLabels[scenario] ?? scenario} com ${integerFormatter.format(quantity)} registros...`);

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
        setStatus(`Medição concluída em ${formatMilliseconds(result.duracaoMs)}.`);
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

        return problem.mensagem || problem.detail || problem.title || `Erro HTTP ${response.status}.`;
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
            queryScenarioMetadata.set(scenario.nome, scenario);

            const option = document.createElement('option');
            option.value = scenario.nome;
            option.textContent = queryScenarioLabels[scenario.nome] ?? scenario.nome;
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

        if (diagnostic.traduzivel) {
            translationBadge.textContent = 'traduzível';
            translationMessage.textContent = diagnostic.mensagem;
        } else {
            translationBadge.textContent = 'falha esperada';
            translationBadge.classList.add('expected');
            translationMessage.textContent = diagnostic.solucaoRecomendada;
            translationMessage.title = diagnostic.mensagem;
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
        card.dataset.queryScenario = scenario.nome;

        const top = document.createElement('div');
        top.className = 'scenario-card-top';

        const title = document.createElement('h4');
        title.textContent = queryScenarioLabels[scenario.nome] ?? scenario.nome;

        const number = document.createElement('span');
        number.className = 'scenario-card-number';
        number.textContent = String(index + 1).padStart(2, '0');
        top.append(title, number);

        const description = document.createElement('p');
        description.className = 'scenario-card-description';
        description.textContent = scenario.objetivo;

        const flow = document.createElement('div');
        flow.className = 'scenario-flow';

        [scenario.consulta, scenario.conversaoEnum, scenario.materializacao].forEach((step) => {
            const item = document.createElement('span');
            item.textContent = step;
            flow.append(item);
        });

        card.append(top, description, flow);
        card.addEventListener('click', () => {
            queryScenarioInput.value = scenario.nome;
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
        queryScenarioDescription.textContent = metadata.objetivo;
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
    const query = new URLSearchParams({
        quantidade: String(quantity),
        aquecer: String(queryWarmupInput.checked),
        forcarGc: String(queryForceGcInput.checked)
    });

    const executionLabel = queryScenarioLabels[scenario] ?? scenario;
    const startedAt = performance.now();
    setQueryLoading(true);
    setQueryStatus(`Executando ${executionLabel} com ${integerFormatter.format(quantity)} registros…`);

    const progressTimer = window.setInterval(() => {
        const elapsedSeconds = Math.floor((performance.now() - startedAt) / 1000);
        setQueryStatus(
            `Executando ${executionLabel} com ${integerFormatter.format(quantity)} registros… ${elapsedSeconds}s. ` +
            'Na primeira execução, o banco pode ser preparado antes da medição.'
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
        setQueryStatus(`Medição concluída em ${formatMilliseconds(result.duracaoMs)}.`);
        queryResults.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (error) {
        setQueryStatus(error.message || 'A medição da consulta falhou.', true);
    } finally {
        window.clearInterval(progressTimer);
        setQueryLoading(false);
    }
}

function renderQueryResult(result) {
    queryResults.classList.remove('hidden');
    document.querySelector('#query-result-title').textContent =
        `${queryScenarioLabels[result.cenario] ?? result.cenario} · ${integerFormatter.format(result.quantidade)} linhas`;
    document.querySelector('#query-managed-peak').textContent = formatMiB(result.deltaPicoMemoriaGerenciadaMiB);
    document.querySelector('#query-working-set').textContent = formatMiB(result.deltaPicoWorkingSetMiB);
    document.querySelector('#query-duration').textContent = formatMilliseconds(result.duracaoMs);
    document.querySelector('#query-allocated').textContent = formatMiB(result.alocadoDuranteMedicaoMiB);
    document.querySelector('#query-file-size').textContent = formatMiB(result.tamanhoArquivoMiB);
    document.querySelector('#query-sql').textContent = result.sqlGerado;

    const flags = document.querySelector('#query-result-flags');
    flags.replaceChildren();
    appendResultFlag(flags, result.bufferizaResultados ? 'bufferiza tudo' : 'streaming', !result.bufferizaResultados);
    appendResultFlag(
        flags,
        result.conversaoEnumNoCliente ? 'enum no cliente' : 'enum no SQL',
        !result.conversaoEnumNoCliente
    );
    appendResultFlag(flags, `${result.quantidadeAmostras} amostras`, false);
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
        ...queryHistory.map((item) => item.deltaPicoMemoriaGerenciadaMiB),
        1
    );
    queryHistoryBars.replaceChildren();

    queryHistory.forEach((item) => {
        const row = document.createElement('div');
        row.className = 'history-bar';

        const label = document.createElement('span');
        label.className = 'history-bar-label';
        label.textContent = `${queryScenarioLabels[item.cenario] ?? item.cenario} · ${integerFormatter.format(item.quantidade)}`;

        const track = document.createElement('div');
        track.className = 'history-bar-track';
        const fill = document.createElement('span');
        fill.style.width = `${Math.max(3, item.deltaPicoMemoriaGerenciadaMiB / maxValue * 100)}%`;
        track.append(fill);

        const value = document.createElement('span');
        value.className = 'history-bar-value';
        value.textContent = formatMiB(item.deltaPicoMemoriaGerenciadaMiB);
        row.append(label, track, value);
        queryHistoryBars.append(row);
    });
}

function saveQueryHistory() {
    try {
        const summaries = queryHistory.map((item) => ({
            cenario: item.cenario,
            quantidade: item.quantidade,
            deltaPicoMemoriaGerenciadaMiB: item.deltaPicoMemoriaGerenciadaMiB
        }));
        sessionStorage.setItem('query-miniexcel-history', JSON.stringify(summaries));
    } catch {
        // O histórico continua funcionando em memória quando o storage não está disponível.
    }
}

function restoreQueryHistory() {
    try {
        const saved = JSON.parse(sessionStorage.getItem('query-miniexcel-history') ?? '[]');

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
    resultsSection.classList.remove('hidden');
    document.querySelector('#result-scenario').textContent = scenarioLabels[result.cenario] ?? result.cenario;
    document.querySelector('#managed-peak').textContent = formatMiB(result.deltaPicoMemoriaGerenciadaMiB);
    document.querySelector('#working-set').textContent = formatMiB(result.deltaPicoWorkingSetMiB);
    document.querySelector('#private-memory').textContent = formatMiB(result.deltaPicoMemoriaPrivadaMiB);
    document.querySelector('#allocated-memory').textContent = formatMiB(result.alocadoDuranteMedicaoMiB);
    document.querySelector('#duration').textContent = formatMilliseconds(result.duracaoMs);
    document.querySelector('#sample-count').textContent = `${integerFormatter.format(result.quantidadeAmostras)} amostras a cada ${result.intervaloAmostragemMs} ms.`;
    document.querySelector('#file-size').textContent = formatMiB(result.tamanhoArquivoMiB);
    document.querySelector('#measurement-target').textContent = result.destinoMedicao;
    document.querySelector('#gc-counts').textContent = `G0 ${result.coletasGeracao0} · G1 ${result.coletasGeracao1} · G2 ${result.coletasGeracao2}`;

    const trackWidth = Math.min(100, Math.max(8, result.deltaPicoMemoriaGerenciadaMiB / 2.5));
    document.querySelector('#managed-track').style.width = `${trackWidth}%`;
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
    const maxValue = Math.max(...history.map((item) => item.deltaPicoMemoriaGerenciadaMiB), 1);
    historyBars.replaceChildren();

    history.forEach((item) => {
        const row = document.createElement('div');
        row.className = 'history-bar';

        const label = document.createElement('span');
        label.className = 'history-bar-label';
        label.textContent = scenarioLabels[item.cenario] ?? item.cenario;

        const track = document.createElement('div');
        track.className = 'history-bar-track';
        const fill = document.createElement('span');
        fill.style.width = `${Math.max(3, item.deltaPicoMemoriaGerenciadaMiB / maxValue * 100)}%`;
        track.append(fill);

        const value = document.createElement('span');
        value.className = 'history-bar-value';
        value.textContent = formatMiB(item.deltaPicoMemoriaGerenciadaMiB);

        row.append(label, track, value);
        historyBars.append(row);
    });
}

function renderHistoryTable() {
    historyTable.replaceChildren();

    history.forEach((item) => {
        const row = document.createElement('tr');
        const values = [
            scenarioLabels[item.cenario] ?? item.cenario,
            integerFormatter.format(item.quantidade),
            formatMiB(item.deltaPicoMemoriaGerenciadaMiB),
            formatMiB(item.deltaPicoWorkingSetMiB),
            formatMilliseconds(item.duracaoMs)
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
