const POP_MAX = 1000
birth_rate = 0.2
death_rate = 0.05
resources = 100
pop_size = 10

function use_resources(population, food)
    return food / (food + population)
end

function α(b, F, N)
    return b * use_resources(N, F)
end

function β(d, F, N)
    return d * use_resources(N, F)
end

transition_matrix = zeros(Float64, POP_MAX, POP_MAX)
for i = 1:POP_MAX
    for j = 1:POP_MAX
        if i == j + 1
            transition_matrix[i, j] = α(birth_rate, resources, pop_size)
        elseif i == j - 1
            transition_matrix[i, j] = β(death_rate, resources, pop_size)
        end
    end
end
