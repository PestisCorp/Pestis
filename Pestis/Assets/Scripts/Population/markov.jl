using StatsBase

const POP_MAX = 1000
birth_rate = 0.5
death_rate = 0.05
resources = 100
pop_size = 1

function consume_resources(N, F)
    F -= ceil(N * 0.4)
    return max(0, F)
end

function resource_weight_growth(population, food)
    return food / (food + population)
end

function resource_weight_decline(population, food)
    return population / (population + food)
end

function α(b, F, N, w)
    return b * resource_weight_growth(N, F) * w
end

function β(d, F, N, w)
    return d * resource_weight_decline(N, F) * w
end

function generate_transition_matrix(W, NMax, N, F, b, d)
    transition_matrix = zeros(Float64, NMax, NMax)
    W_MIN = minimum(W)
    W_MAX = maximum(W)
    for i = 1:NMax
        for j = 1:5
            w = (W[j] - W_MIN) / (W_MAX - W_MIN)
            if i + j <= NMax
                αj = α(b, F, N, w)
                transition_matrix[i, i+j] = αj
            end
            if i - j >= 1
                βj = β(d, F, N, w)
                transition_matrix[i, i-j] = βj
            end
        end
        transition_matrix[i, i] = max(1 - sum(transition_matrix[i, :]), 0.0)
    end
    foreach(StatsBase.normalize!, eachrow(transition_matrix))
    return transition_matrix
end

function simulate_population(NMax, N, F, b, d)
    W = [5, 4, 3, 2, 1]
    while true
        transition_matrix = generate_transition_matrix(W, NMax, N, F, b, d)
        if N <= 0
            println("Population has gone extinct.")
            return
        end
        println("Population: ", N, "\nResources: ", F)
        probs = transition_matrix[N, :]
        next_state = StatsBase.sample(1:NMax, StatsBase.ProbabilityWeights(probs))
        F = consume_resources(N, F)
        N = next_state
        sleep(1)
    end
end

simulate_population(POP_MAX, pop_size, resources, birth_rate, death_rate)
