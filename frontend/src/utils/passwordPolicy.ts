export const PASSWORD_POLICY_HINT =
  'Usa entre 12 y 128 caracteres, con mayúscula, minúscula y número.';

export const getPasswordPolicyError = (password: string): string | undefined => {
  if (password.length < 12 || password.length > 128) return PASSWORD_POLICY_HINT;
  if (!/\p{Lu}/u.test(password) || !/\p{Ll}/u.test(password) || !/\p{Nd}/u.test(password)) {
    return PASSWORD_POLICY_HINT;
  }
  return undefined;
};
