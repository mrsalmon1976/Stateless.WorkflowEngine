import ConfirmModal from '../components/ConfirmModal.vue'

/**
 * Shows a confirmation modal, resolving to true if the user confirms.
 */
export function useConfirm() {
  const overlay = useOverlay()
  const modal = overlay.create(ConfirmModal)

  async function confirm({ title, description, confirmLabel = 'Confirm', color = 'primary' }) {
    const instance = modal.open({ title, description, confirmLabel, color })
    return (await instance.result) === true
  }

  return { confirm }
}
