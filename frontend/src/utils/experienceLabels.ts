const difficultyLabels: Record<string, string> = {
  Easy: 'Fácil',
  Moderate: 'Moderada',
  Demanding: 'Exigente',
};

const cancellationPolicyLabels: Record<string, string> = {
  Flexible: 'Flexible',
  Moderate: 'Moderada',
  Strict: 'Estricta',
};

export const getDifficultyLabel = (difficulty: string) => difficultyLabels[difficulty] ?? difficulty;

export const getCancellationPolicyLabel = (policy: string) => cancellationPolicyLabels[policy] ?? policy;
