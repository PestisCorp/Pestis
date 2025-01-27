const POP_MAX = 20
birth_rate = 0.2
death_rate = 0.05
resources = 100
pop_size = 10


function use_resources(population, food)
    return food / (food + population)
end

function α(b, F, N, w)
    return b * use_resources(N, F) * w
end

function β(d, F, N, w)
    return d * use_resources(N, F) * w
end

transition_matrix = zeros(Float64, POP_MAX, POP_MAX)
W = [5, 4, 3, 2, 1]
W_MIN = minimum(W)
W_MAX = maximum(W)
for i = 1:POP_MAX
    σ = 1
    for j = 1:5
        if i + j > POP_MAX || i - j < 1
            continue
        end
        w = (W[j] - W_MIN) / (W_MAX - W_MIN)
        αj = α(birth_rate, resources, pop_size, w)
        βj = β(death_rate, resources, pop_size, w)
        σ -= αj + βj
        transition_matrix[i, i+j] = αj
        transition_matrix[i, i-j] = βj
    end
    transition_matrix[i, i] = σ
end
display(transition_matrix)
